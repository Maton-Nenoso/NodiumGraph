# NodiumGraph Core Types Design

**Date:** 2026-04-08
**Status:** Approved

## Architecture Changes

### Single Project

The original two-project split (`NodiumGraph.Core` + `NodiumGraph.Avalonia`) is merged into a single `NodiumGraph` project. Since this is an Avalonia extension library, every consumer is already an Avalonia app — keeping Core Avalonia-free just creates friction with custom geometric types and conversions.

- Single project: `src/NodiumGraph/`
- Sub-namespaces by concern: `NodiumGraph` (root utilities), `NodiumGraph.Model`, `NodiumGraph.Interactions`, `NodiumGraph.Controls`
- References Avalonia directly — uses `Avalonia.Point`, `IBrush`, `IDashStyle`
- Single test project: `tests/NodiumGraph.Tests/`

### Base Classes Over Interfaces for Models

Model types (`Node`, `Port`, `Connection`) are concrete classes, not interfaces. Consumers subclass `Node` to add domain data, or use it directly for simple connectors. `Port` and `Connection` work out of the box and are subclassable for rich cases.

## Type Inventory (18 types)

### Model Classes (concrete, unsealed)

#### Node

```csharp
public class Node : INotifyPropertyChanged
{
    public Guid Id { get; }
    public double X { get; set; }           // fires PropertyChanged
    public double Y { get; set; }           // fires PropertyChanged
    public double Width { get; internal set; }
    public double Height { get; internal set; }
    public IPortProvider? PortProvider { get; set; }
}
```

- Implements `INotifyPropertyChanged`, fires on X/Y changes
- Width/Height are `internal set` — library measures rendered control, consumer reads
- PortProvider set per instance — consumer sets in constructor of derived class or on the instance directly

#### Port

```csharp
public class Port
{
    public Guid Id { get; }
    public Node Owner { get; }
    public Point Position { get; set; }         // relative to owner's top-left
    public Point AbsolutePosition =>            // computed
        new(Owner.X + Position.X, Owner.Y + Position.Y);
}
```

- Position is relative to owning node's top-left corner
- AbsolutePosition is computed on demand from owner's current position
- When node moves, connection rendering reads updated AbsolutePosition automatically

#### Connection

```csharp
public class Connection
{
    public Guid Id { get; }
    public Port SourcePort { get; }
    public Port TargetPort { get; }
}
```

#### Graph

```csharp
public class Graph
{
    public ObservableCollection<Node> Nodes { get; }
    public ObservableCollection<Connection> Connections { get; }
    public IReadOnlyList<Node> SelectedNodes { get; }
    public void AddNode(Node node);
    public void RemoveNode(Node node);              // cascades: removes connected connections
    public void AddConnection(Connection connection);
    public void RemoveConnection(Connection connection);
}
```

- Canvas binds to a `Graph` instance
- ObservableCollection provides change notifications for add/remove
- RemoveNode cascades to connections — removes any connection referencing the node's ports

### Port Provider Strategy

#### IPortProvider (interface)

```csharp
public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position);
}
```

- `ResolvePort` returns the best port for a given position, or null if none in range
- Library calls this during connection drag

#### FixedPortProvider (concrete)

- Consumer declares ports at fixed positions
- `ResolvePort()` returns nearest port within hit-test radius
- Ports list is set at construction time

#### DynamicPortProvider (concrete)

- Creates ports at node boundary intersection
- `ResolvePort()` reuses an existing port if within distance threshold
- Otherwise creates a new port at the boundary point
- Ports list grows as connections are made

### Result Pattern

#### Error

```csharp
public record Error(string Message, string? Code = null);
```

#### Result

```csharp
public record Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error);

    public static Result Success();
    public static Result Failure(Error error);

    public static implicit operator Result(Error error);
}
```

#### Result\<T\>

```csharp
public record Result<T> : Result
{
    public T? Value { get; }

    public static implicit operator Result<T>(T value);
    public static implicit operator Result<T>(Error error);
}
```

### Handler Interfaces

Set as nullable properties on the canvas. Consumer implements only the ones they need.

#### INodeInteractionHandler

```csharp
public interface INodeInteractionHandler
{
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);
    void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections);
    void OnNodeDoubleClicked(Node node);
}
```

#### IConnectionHandler

```csharp
public interface IConnectionHandler
{
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
```

#### ISelectionHandler

```csharp
public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyList<Node> selectedNodes);
}
```

#### ICanvasInteractionHandler

```csharp
public interface ICanvasInteractionHandler
{
    void OnCanvasDoubleClicked(Point worldPosition);
    void OnCanvasDropped(Point worldPosition, object data);
}
```

### Strategy Interfaces

#### IConnectionValidator

```csharp
public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
```

#### IConnectionRouter

```csharp
public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
}
```

#### IConnectionStyle (interface + default)

```csharp
public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}

public class ConnectionStyle : IConnectionStyle
{
    // Concrete default with sensible values
}
```

### Supporting Types

#### NodeMoveInfo

```csharp
public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
```

## Connection Rendering Flow

When a node moves:
1. `Node.X`/`Y` changes → fires `PropertyChanged`
2. Canvas receives notification → invalidates render
3. During render, canvas iterates connections → calls `IConnectionRouter.Route(source, target)`
4. Router reads `Port.AbsolutePosition` (computed from current node position)
5. Connection path drawn through updated points

No extra subscriptions, no lookup tables. Positions are always computed from current node state.

## Scope

This design covers the type definitions only. Canvas control implementation (bindable properties, rendering layers, input handling, hit-testing) is a separate design cycle that depends on these types being in place.
