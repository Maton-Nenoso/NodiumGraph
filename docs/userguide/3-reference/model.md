# Model Reference

The NodiumGraph model is four concrete, unsealed base classes — `Graph`, `Node`, `Port`, `Connection` — plus a handful of supporting types. All live in `NodiumGraph.Model`. Consumers typically subclass `Node` (and occasionally `Connection`) to attach domain data; `Graph` and `Port` are normally used as-is. `Node` and `Port` implement `INotifyPropertyChanged`; the canvas listens to those notifications to keep its rendering in sync.

## Graph

Namespace: `NodiumGraph.Model`

The top-level container. It owns the node and connection collections and tracks the current selection set. All structural mutations must go through its methods — the exposed collections are read-only wrappers so that the canvas can rely on `ObservableCollection` semantics without consumers bypassing invariants.

### Properties

| Property | Type | Description |
|---|---|---|
| `Nodes` | `ReadOnlyObservableCollection<Node>` | All nodes in the graph. Mutate via `AddNode` / `RemoveNode` / `RemoveNodes`. |
| `Connections` | `ReadOnlyObservableCollection<Connection>` | All connections in the graph. Mutate via `AddConnection` / `RemoveConnection`. |
| `SelectedNodes` | `IReadOnlyList<Node>` | Current selection. Driven by `Select` / `Deselect` / `ClearSelection` (and by the canvas when the user clicks or marquee-selects). |

### Methods

| Member | Signature | Description |
|---|---|---|
| `AddNode` | `void AddNode(Node node)` | Adds a node to the graph. Throws `InvalidOperationException` if the node is already in the graph. |
| `RemoveNode` | `void RemoveNode(Node node)` | Removes a node and cascades to every connection touching any of its ports. Also clears the node's selection state. |
| `RemoveNodes` | `void RemoveNodes(IEnumerable<Node> nodes)` | Removes multiple nodes and all cascaded connections in a single pass. Silently skips nodes that are not part of this graph. |
| `AddConnection` | `void AddConnection(Connection connection)` | Adds a connection. Throws `InvalidOperationException` if the connection is already in the graph or if either endpoint's owner node is not in the graph. |
| `RemoveConnection` | `void RemoveConnection(Connection connection)` | Removes a connection. No-op if the connection is not in the graph (idempotent). |
| `Select` | `void Select(Node node)` | Adds `node` to `SelectedNodes` and sets its `IsSelected` flag. Throws if the node is not part of this graph. Idempotent for already-selected nodes. |
| `Deselect` | `void Deselect(Node node)` | Removes `node` from `SelectedNodes`. Idempotent. |
| `ClearSelection` | `void ClearSelection()` | Empties `SelectedNodes` and clears every node's `IsSelected` flag. |

### Usage

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
var graph = new Graph();

var source = CreateMathNode("Source", "Produces a number", x: 120, y: 200);
var sink = CreateMathNode("Sink", "Consumes a number", x: 480, y: 200);

graph.AddNode(source);
graph.AddNode(sink);

var sourceOut = source.PortProvider!.Ports[1];
var sinkIn = sink.PortProvider!.Ports[0];
graph.AddConnection(new Connection(sourceOut, sinkIn));
```

> **Gotcha:** `Nodes` and `Connections` are read-only from the consumer's perspective. Never attempt to mutate these collections directly — always go through the `Add*` / `Remove*` methods, which maintain selection state, cascade, and invariants.

## Node

Namespace: `NodiumGraph.Model`

The base class for graph elements. Subclass this to attach domain data.

### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Id` | `Guid` | new `Guid.NewGuid()` | Stable per-instance identifier assigned at construction. Read-only. |
| `X` | `double` | `0` | World-space X coordinate (top-left). |
| `Y` | `double` | `0` | World-space Y coordinate (top-left). |
| `Width` | `double` | `0` | Measured width. Setter is `internal` — the canvas assigns this after measure. |
| `Height` | `double` | `0` | Measured height. Setter is `internal` — the canvas assigns this after measure. |
| `Title` | `string` | `GetType().Name` | Title shown by the default node template when `ShowHeader` is `true`. |
| `IsSelected` | `bool` | `false` | Selection flag. Setter is `internal` — driven by `Graph.Select` / `Deselect`. |
| `ShowHeader` | `bool` | `true` | Controls whether the default template renders the header bar. |
| `IsCollapsible` | `bool` | `false` | Whether the default template renders a collapse toggle. |
| `IsCollapsed` | `bool` | `false` | When `true`: ports are hidden, not hit-testable, and new connections are blocked. Consumer sets this — there is no built-in collapse button. |
| `Style` | `NodeStyle?` | `null` | Per-instance visual overrides. Null properties fall through to theme, then default. |
| `Shape` | `INodeShape` | `new RectangleShape()` | Geometric boundary used for angle-based port positioning. |
| `PortProvider` | `IPortProvider?` | `null` | Strategy that creates and resolves this node's ports. See [strategies reference](strategies.md). |

### Events

- `PropertyChanged` — inherited from `INotifyPropertyChanged`. Fires when any consumer-settable property changes. The canvas listens to this to recompute `AbsolutePosition` on every owned port and invalidate visuals.

### Subclassing

```csharp
// from: samples/GettingStarted/MathNode.cs
public class MathNode : Node
{
    public string Description { get; set; } = string.Empty;
}
```

> **Gotcha:** `Width`, `Height`, and `IsSelected` all have `internal` setters — they are driven by the canvas (measure pass) and by `Graph` (selection state), not by consumer code. `Style` is applied when the node's `DataTemplate` is first created; changing its properties at runtime does not rebuild the template. To force a style refresh, remove and re-add the node.

## Port

Namespace: `NodiumGraph.Model`

The connection attachment point on a node.

### Constructors

```csharp
public Port(Node owner, string name, PortFlow flow, Point position);
public Port(Node owner, Point position); // name = "", flow = PortFlow.Input
```

The single-argument form is the shortcut used by dynamic providers that don't need names or flows. Both forms subscribe to the owner node's `PropertyChanged` so that `AbsolutePosition` invalidates when the node moves.

### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Id` | `Guid` | new `Guid.NewGuid()` | Stable per-instance identifier. Read-only. |
| `Owner` | `Node` | (ctor) | The node this port belongs to. Read-only. |
| `Name` | `string` | (ctor) | Stable string identifier scoped to the owner. Read-only. |
| `Flow` | `PortFlow` | (ctor) | `Input` or `Output`. Read-only and semantic only — it does not dictate position on the node. |
| `Position` | `Point` | (ctor) | Node-local coordinates (relative to the node's top-left). Setter is `internal` — providers assign it during layout. |
| `AbsolutePosition` | `Point` | computed | World-space position: `Owner.X + Position.X`, `Owner.Y + Position.Y`. Cached and invalidated on owner move or position change. Read-only. |
| `Label` | `string?` | `null` | Optional text label rendered next to the port when no custom `PortTemplate` is set. |
| `MaxConnections` | `uint?` | `null` | Metadata for validators. The library itself never enforces this. |
| `DataType` | `object?` | `null` | Opaque type token consumed by `IConnectionValidator`. The default validator compares with `Equals`. Prefer reference-typed tokens (strings, `System.Type`, records) to avoid boxing during drag. |
| `Style` | `PortStyle?` | `null` | Per-instance visual overrides. Null properties fall through to theme, then default. |

### Events

- `PropertyChanged` — inherited from `INotifyPropertyChanged`. Fires for `Position`, `AbsolutePosition`, `Style`, `Label`, `MaxConnections`, and `DataType`.

### PortFlow enum

```csharp
public enum PortFlow { Input, Output }
```

> **Gotcha:** `PortFlow` is a *semantic* marker (direction of data) — it is orthogonal to where the port sits on the node. The default validator uses `Flow` to reject `Input`-to-`Input` and `Output`-to-`Output` pairs. `Position` is node-local; when you need canvas / world coordinates, use `AbsolutePosition`.

## Connection

Namespace: `NodiumGraph.Model`

A directed link between two ports. Subclass for labels, weights, or other per-edge data.

### Constructor and properties

```csharp
public Connection(Port sourcePort, Port targetPort);
```

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Stable per-instance identifier assigned at construction. |
| `SourcePort` | `Port` | Read-only. Throws `ArgumentNullException` if `null`. |
| `TargetPort` | `Port` | Read-only. Throws `ArgumentNullException` if `null`. |

`Connection` itself enforces nothing about directionality — source is *typically* a `PortFlow.Output` and target is *typically* a `PortFlow.Input`, but that constraint is enforced by `IConnectionValidator`, not by this class.

## NodeMoveInfo

Namespace: `NodiumGraph.Model`

```csharp
public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
```

A snapshot of a node's position before and after a drag. Passed to `INodeInteractionHandler.OnNodesMoved` after a drag completes, carrying the information an undo/redo stack needs without the library having to retain any history itself. The `Node` reference may outlive its presence in the graph — consumers storing these for undo must be prepared for the node to have been removed before the undo is applied.

## See also

- [Handler interfaces reference](handlers.md) — where `Graph.SelectedNodes` is reported and `NodeMoveInfo` is consumed
- [Strategy interfaces reference](strategies.md) — port providers live here
- [Canvas control reference](canvas-control.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
