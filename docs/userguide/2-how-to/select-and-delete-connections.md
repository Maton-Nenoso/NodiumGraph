# Select and Delete Connections

## Goal

Let users click, ctrl-click, and marquee-select connections alongside nodes, then delete the mixed selection with the Delete key — all routed through your handler code so you control what actually happens (undo grouping, validation, side effects).

## Prerequisites

- You already host `NodiumGraphCanvas` with a `Graph` that contains nodes and connections. See [Host the Canvas](host-canvas.md).
- You understand `ISelectionHandler` and how node selection works today. See [Strategy interfaces reference](../3-reference/strategies.md).

## Steps

### 1. Understand the unified selection model

Selection is driven by `Graph.SelectedItems`, an `ObservableCollection<IGraphElement>` that holds both `Node` and `Connection` instances. Two read-only mirror views partition it by type:

```csharp
Graph.SelectedItems         // ObservableCollection<IGraphElement> — canonical
Graph.SelectedNodes         // ReadOnlyObservableCollection<Node> — filtered view
Graph.SelectedConnections   // ReadOnlyObservableCollection<Connection> — filtered view
```

The mirror views update automatically when `SelectedItems` changes. Consumer code that only cares about nodes can read `SelectedNodes` without filtering.

### 2. Handle selection changes

`ISelectionHandler.OnSelectionChanged` receives the full mixed selection as `IReadOnlyCollection<IGraphElement>`. Use `.OfType<T>()` to partition:

```csharp
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class MySelectionHandler : ISelectionHandler
{
    public void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected)
    {
        var nodes = selected.OfType<Node>().ToList();
        var connections = selected.OfType<Connection>().ToList();

        // Update your property panel, inspector, status bar, etc.
        Console.WriteLine($"Selected: {nodes.Count} nodes, {connections.Count} connections");
    }
}
```

Wire it on the canvas:

```csharp
Canvas.SelectionHandler = new MySelectionHandler();
```

### 3. Built-in selection interactions

These work for both nodes and connections — no additional wiring needed:

| Input | Result |
|---|---|
| Click on a node | `SelectedItems = { node }` |
| Click on a connection | `SelectedItems = { connection }` |
| Click on empty canvas | `SelectedItems` cleared |
| Ctrl+click on any element | Toggle that element in/out of `SelectedItems` |
| Marquee drag | All nodes and connections intersecting the rectangle are selected |
| Ctrl+marquee drag | Same, but additive to existing selection |

Connection hit-testing uses a screen-space-constant pixel tolerance (8px) that scales with zoom, so connections are equally easy to click at any zoom level. When a port and a connection overlap, the port wins — clicking starts a connection draw, not a selection.

### 4. Wire the unified delete handler

The Delete key fires `IGraphInteractionHandler.OnDeleteRequested` with the full `SelectedItems` snapshot. Implement this interface to control what happens:

```csharp
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class MyGraphInteractionHandler(Graph graph) : IGraphInteractionHandler
{
    public void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)
    {
        // Remove connections first so node-cascade doesn't double-remove.
        foreach (var connection in elements.OfType<Connection>().ToList())
            graph.RemoveConnection(connection);

        foreach (var node in elements.OfType<Node>().ToList())
            graph.RemoveNode(node);
    }
}
```

Wire it on the canvas:

```csharp
Canvas.GraphInteractionHandler = new MyGraphInteractionHandler(graph);
```

The library snapshots `SelectedItems` before firing the callback, so mutating the graph from inside `OnDeleteRequested` is safe — the snapshot won't change under your feet.

### 5. Add undo support

Since `OnDeleteRequested` is your code, wrap the deletions in an undo group:

```csharp
public void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)
{
    using var undo = _undoManager.BeginGroup("Delete selection");

    foreach (var connection in elements.OfType<Connection>().ToList())
    {
        _undoManager.Record(new RemoveConnectionAction(connection));
        _graph.RemoveConnection(connection);
    }

    foreach (var node in elements.OfType<Node>().ToList())
    {
        _undoManager.Record(new RemoveNodeAction(node));
        _graph.RemoveNode(node);
    }
}
```

The library doesn't own undo/redo — it just gives you the right entry point to build it.

### 6. Selection halo rendering

Selected connections automatically render a selection halo — a wider, semi-transparent stroke drawn beneath the normal connection stroke. This uses the `ConnectionSelectionHaloBrushKey` theme resource from `Generic.axaml`. You can override it in your theme to match your app's accent color:

```xml
<Style Selector="ng|NodiumGraphCanvas">
  <Style.Resources>
    <SolidColorBrush x:Key="{x:Static ng:NodiumGraphResources.ConnectionSelectionHaloBrushKey}"
                     Color="#3B82F6" Opacity="0.35" />
  </Style.Resources>
</Style>
```

## Full code

```csharp
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

// Selection handler — reacts to selection changes
public sealed class MySelectionHandler : ISelectionHandler
{
    public void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected)
    {
        var nodes = selected.OfType<Node>().ToList();
        var connections = selected.OfType<Connection>().ToList();
        // Update UI: property panel, status bar, etc.
    }
}

// Delete handler — controls what happens on Delete key
public sealed class MyGraphInteractionHandler(Graph graph) : IGraphInteractionHandler
{
    public void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)
    {
        foreach (var connection in elements.OfType<Connection>().ToList())
            graph.RemoveConnection(connection);
        foreach (var node in elements.OfType<Node>().ToList())
            graph.RemoveNode(node);
    }
}

// Wiring in MainWindow.axaml.cs:
Canvas.SelectionHandler = new MySelectionHandler();
Canvas.GraphInteractionHandler = new MyGraphInteractionHandler(graph);
```

## Gotchas

- **Remove connections before nodes.** When you remove a node, the library cascade-removes its connections from the graph and from `SelectedItems`. If you remove nodes first and then try to remove their connections, you'll be calling `RemoveConnection` on connections already gone. Connections-first avoids the double-remove.
- **`SelectedNodes` is now `ReadOnlyObservableCollection<Node>`.** You can't add or remove from it directly. Mutate `Graph.SelectedItems` instead — the mirror view updates automatically.
- **`INodeInteractionHandler.OnDeleteRequested` no longer exists.** Delete handling is unified through `IGraphInteractionHandler.OnDeleteRequested`. If you previously implemented `INodeInteractionHandler.OnDeleteRequested`, move that logic into the new handler and add connection handling alongside it.
- **`IConnectionHandler.OnConnectionDeleteRequested` is separate.** That callback handles single-connection imperative deletes (e.g. a context menu "remove connection" button). The unified `OnDeleteRequested` handles keyboard-triggered batch deletes of the current selection. Both can coexist.
- **Port clicks take priority over connection clicks.** If a port visually overlaps a connection line, clicking starts a connection draw — it doesn't select the connection. This is intentional: connection drawing is the more common action at a port.
- **Marquee picks connections by intersection, not containment.** A connection only needs to cross the marquee rectangle to be selected — it doesn't need to be fully enclosed. This matches how most graph editors behave.
- **Cast to `INotifyCollectionChanged` for binding.** `SelectedNodes` and `SelectedConnections` are `ReadOnlyObservableCollection<T>`, which in Avalonia 12 requires casting to `INotifyCollectionChanged` for `CollectionChanged` events: `((INotifyCollectionChanged)graph.SelectedNodes).CollectionChanged += ...`.

## See also

- [IGraphInteractionHandler reference](../3-reference/connection-api.md)
- [Rendering pipeline reference](../3-reference/rendering-pipeline.md)
- [Style connections with arrowheads](style-connections-with-arrowheads.md)
- [Custom connection style](custom-style.md)
- [Theme the canvas](theme-canvas.md)
