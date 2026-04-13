# Subclass `Node` and `Connection` for Domain Data

## Goal

Attach your own fields to nodes and connections — the formula of a computation node, the weight of an edge, a reference to a domain entity — while keeping the canvas rendering them correctly.

## Prerequisites

- You already host `NodiumGraphCanvas` and have a `Graph`. See [Host the Canvas](host-canvas.md).
- You understand that NodiumGraph's [architecture](../4-explanation/architecture.md) is "concrete base classes for the model, interfaces for the strategies". Subclassing is the intended extension path for `Node` / `Connection`; for routing, validation, styling, you implement an interface instead.

## Steps

### 1. Why subclassing is the right extension point for the model

`Node` and `Connection` are unsealed, concrete classes. The library never stores domain data itself — it does not ship "tags", "metadata", or "user-data" properties — so every real app subclasses them to add its own state.

Two forces push this extension toward subclassing rather than composition:

- **Avalonia DataTemplate resolution is type-keyed.** A `DataTemplate DataType="local:MathNode"` only matches instances whose runtime type is `MathNode`. Holding a reference to a sibling object from a plain `Node` would make templates far harder to target.
- **INPC already exists on `Node`.** The base implements `INotifyPropertyChanged` and exposes a protected `SetField<T>` helper. Subclass properties that want UI-bound live updates can use it directly — there is no plumbing to add.

### 2. Subclass `Node` with simple data fields

For nodes whose extra state does not change after construction, plain auto-properties are sufficient:

```csharp
using NodiumGraph.Model;

public class MathNode : Node
{
    public string Description { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
}
```

This is what the Getting Started sample uses. Bindings in the DataTemplate (`{Binding Description}`, `{Binding Formula}`) read the properties at template instantiation, which is the moment each node first appears on the canvas.

### 3. Subclass `Node` with INPC-aware properties

For fields users can edit at runtime — through an inspector, a side-panel form, an external event — you want the UI to update immediately. Use the protected `SetField` helper from the base class:

```csharp
using NodiumGraph.Model;

public class MathNode : Node
{
    private string _description = string.Empty;
    private string _formula = string.Empty;

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string Formula
    {
        get => _formula;
        set => SetField(ref _formula, value);
    }
}
```

`SetField` uses `EqualityComparer<T>.Default` to skip redundant writes, sets the backing field, and raises `PropertyChanged` with the caller-member-name automatically. This is identical to the base-class pattern used for `Title`, `X`, `Y`, `ShowHeader`, etc.

### 4. Subclass `Connection` for labels and weights

`Connection` is even simpler — the base class holds `Id`, `SourcePort`, and `TargetPort`, and nothing else. To attach data, subclass and add properties:

```csharp
using NodiumGraph.Model;

public class WeightedConnection : Connection
{
    public double Weight { get; set; }
    public string Label { get; set; } = string.Empty;

    public WeightedConnection(Port source, Port target, double weight)
        : base(source, target)
    {
        Weight = weight;
    }
}
```

Unlike `Node`, `Connection` does **not** implement `INotifyPropertyChanged` on the base. If you want edit-time updates on a connection property to repaint anything, you either:

1. Implement `INotifyPropertyChanged` yourself on the subclass and raise it from setters, or
2. Live with the fact that connection property changes only show up when the canvas next invalidates (e.g., when a touching node moves).

Connection visuals today render via `IConnectionStyle` and `IConnectionRouter` — neither of which reads from the `Connection` instance's own properties — so in practice most apps don't need INPC on connections.

### 5. Add connection-specific behaviour without subclassing `Connection`

You can achieve a lot without touching `Connection` at all:

- **Per-connection labels overlaid on the canvas** — render labels from your own adorner layer by iterating `graph.Connections` and projecting midpoints.
- **Weights influencing routing** — a custom `IConnectionRouter` has access to `Port source` and `Port target`, but not to the `Connection` instance. If you need the weight in routing, use a side-channel map from `(source, target) → weight`, or cast inside the router if you know the concrete type.
- **Custom style per connection** — not currently supported at the `IConnectionStyle` layer. See the [custom style recipe](custom-style.md) for the scope of that interface.

Subclass `Connection` when the data is intrinsic to the edge (weight, label, creation timestamp, domain-entity reference), not when it's presentational.

### 6. Register the template against the concrete type

Once you have a subclass, the DataTemplate keys off its CLR type:

```xml
<Window.DataTemplates>
  <DataTemplate DataType="local:MathNode">
    <ng:NodePresenter HeaderBackground="#6366F1" HeaderForeground="White">
      <TextBlock Text="{Binding Description}" Margin="12,8" />
    </ng:NodePresenter>
  </DataTemplate>
</Window.DataTemplates>
```

If you have multiple subclasses, declare multiple templates. The canvas picks the right one automatically by matching each node's runtime type.

## Full code

```csharp
using Avalonia;
using NodiumGraph.Model;

public class MathNode : Node
{
    private string _description = string.Empty;
    private string _formula = string.Empty;

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string Formula
    {
        get => _formula;
        set => SetField(ref _formula, value);
    }
}

public class WeightedConnection : Connection
{
    public double Weight { get; }
    public string Label { get; }

    public WeightedConnection(Port source, Port target, double weight, string label)
        : base(source, target)
    {
        Weight = weight;
        Label = label;
    }
}
```

Usage in the graph-building code:

```csharp
var graph = new Graph();
var add = new MathNode { Title = "Add", Description = "a + b", X = 120, Y = 200 };
var mul = new MathNode { Title = "Multiply", Description = "a * b", X = 480, Y = 200 };
graph.AddNode(add);
graph.AddNode(mul);
// ...port setup...
graph.AddConnection(new WeightedConnection(add.PortProvider!.Ports[1], mul.PortProvider!.Ports[0], 1.0, "default"));
```

## Gotchas

- **Do not override `Width` / `Height`.** They are declared with `internal set` and written by the canvas after it measures the node's DataTemplate. Any value you assign from outside will be overwritten on the next layout pass. Size the template, not the model.
- **Target the leaf type in DataTemplates.** `DataType="m:Node"` matches *every* subclass — use it only as a fallback. Concrete-type templates (`DataType="local:MathNode"`) take precedence by Avalonia's resolution rules.
- **Use `SetField` on the base for INPC, don't re-implement.** The base class already calls `OnPropertyChanged` with `[CallerMemberName]`. Writing your own raise-propertychanged plumbing duplicates work and is easier to get wrong (forgotten events, wrong property names).
- **`Connection` has no INPC.** Adding it is your responsibility if you need it. Most apps don't — connection visuals today are computed from ports and global style, not from connection properties.
- **Avoid behaviour-heavy subclasses.** Node and Connection are meant to hold *data*. Domain logic — "compute my output from my inputs" — belongs in a separate layer that walks the graph, not in methods on the node class. Keeping the subclass anemic makes persistence, inspection, and testing easier.
- **Don't leak the canvas into your subclass.** `Node` knows nothing about `NodiumGraphCanvas` — it doesn't hold a reference, doesn't call into it, doesn't import its namespace. Keep it that way. Subclasses that depend on the canvas become impossible to use headlessly (batch builds, tests, exports).
- **`PortProvider` is a plain property, not an injection point.** You can assign it in the subclass constructor if every instance of the subclass has the same port layout, but the library does not call a virtual "BuildPorts" method for you. Either set it in the constructor or set it from the code that creates the node.

## See also

- [Model reference](../3-reference/model.md)
- [Define a custom node DataTemplate](custom-node-template.md)
- [Custom port provider](custom-port-provider.md)
- [Persist and restore graph state](persist-graph-state.md)
- [Architecture](../4-explanation/architecture.md)
