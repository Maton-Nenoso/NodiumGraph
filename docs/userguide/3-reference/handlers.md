# Handler Interfaces Reference

Handlers let your application respond to user interactions on the canvas. Per the [report, don't decide](../4-explanation/report-dont-decide.md) philosophy, the canvas calls your handler methods describing what happened — your code decides what to do, including whether to mutate the `Graph`. Every handler property on `NodiumGraphCanvas` is nullable; leaving a handler unset means the corresponding interaction is observed but never acted on.

All four handler interfaces live in `NodiumGraph.Interactions`. None of them are fire-and-forget async — all members are called on the Avalonia UI thread, synchronously, during pointer / keyboard event processing.

## INodeInteractionHandler

Namespace: `NodiumGraph.Interactions`

```csharp
public interface INodeInteractionHandler
{
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);
    void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections);
    void OnNodeDoubleClicked(Node node);
}
```

### `OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves)`

- **When:** after the user completes a node drag. Fires once per drag, never per pointer-move event. If the drag covered a multi-selection, the list contains every affected node.
- **Argument:** a list of `NodeMoveInfo` records, each carrying the node plus both its old and new world-space positions. See the [model reference](model.md#nodemoveinfo).
- **Return:** `void`. A typical implementation pushes the moves onto an undo stack.
- **Threading:** UI thread.
- **Defensive-copy contract:** if you store `moves` past the return of this method, copy it first — the library may reuse the backing list on a subsequent drag.

### `OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections)`

- **When:** the user presses Delete on a selection, or triggers an equivalent gesture.
- **Argument:** the nodes and the connections the user wants to delete.
- **Return:** `void`. The handler is responsible for calling `graph.RemoveNode(...)` / `graph.RemoveConnection(...)` if the deletion should proceed. The library never mutates the graph itself.

### `OnNodeDoubleClicked(Node node)`

- **When:** the user double-clicks a node's body (not a port).
- **Return:** `void`. Typical use is opening a property editor or inspector.

## IConnectionHandler

Namespace: `NodiumGraph.Interactions`

```csharp
public interface IConnectionHandler
{
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
```

### `OnConnectionRequested(Port source, Port target) → Result<Connection>`

- **When:** the user completes a connection drag from `source` onto `target`. Only fires after `IConnectionValidator.CanConnect` returned `true` for the pair during the drag.
- **Return:** `Result<Connection>` — a `Connection` (via the implicit success operator) to accept the request, or an `Error` (via the implicit failure operator) to reject. The library only checks `Result.IsSuccess`; it does not consume `Result<T>.Value`. See the [result pattern reference](result-pattern.md).
- **Critical:** if you accept the request, you must add the connection to your `Graph` yourself inside this method. The canvas does not auto-add — it only reports the request.

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
file sealed class GraphConnectionHandler(Graph graph) : IConnectionHandler
{
    public Result<Connection> OnConnectionRequested(Port source, Port target)
    {
        var connection = new Connection(source, target);
        graph.AddConnection(connection);
        return connection;
    }

    public void OnConnectionDeleteRequested(Connection connection)
    {
        graph.RemoveConnection(connection);
    }
}
```

### `OnConnectionDeleteRequested(Connection connection)`

- **When:** the user triggers a delete on a connection (Delete key on a selected connection, right-drag cut gesture, etc.).
- **Return:** `void`. The handler calls `graph.RemoveConnection(...)` if deletion should proceed.

## ISelectionHandler

Namespace: `NodiumGraph.Interactions`

```csharp
public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyList<Node> selectedNodes);
}
```

### `OnSelectionChanged(IReadOnlyList<Node> selectedNodes)`

- **When:** the selection set changes via click, Ctrl+click, marquee, Ctrl+marquee, or programmatic clear.
- **Argument:** the full post-change selection set. Not a delta.
- **Return:** `void`.
- **Defensive-copy contract:** copy the list if you intend to retain it past the return of this call.

## ICanvasInteractionHandler

Namespace: `NodiumGraph.Interactions`

```csharp
public interface ICanvasInteractionHandler
{
    void OnCanvasDoubleClicked(Point worldPosition);
    void OnCanvasDropped(Point worldPosition, IDataTransfer data);
}
```

### `OnCanvasDoubleClicked(Point worldPosition)`

- **When:** the user double-clicks on empty canvas space (not on a node or port).
- **Argument:** world-space position — the canvas has already undone `ViewportOffset` / `ViewportZoom` for you.
- **Return:** `void`. A common use is spawning a new node at the click location.

### `OnCanvasDropped(Point worldPosition, IDataTransfer data)`

- **When:** an external drag-drop completes onto the canvas. The canvas opts into Avalonia's `DragDrop.AllowDrop` automatically.
- **Argument:** world-space position plus the Avalonia `IDataTransfer` payload.
- **Return:** `void`. The handler decodes the data and creates whatever nodes / connections it represents.

## See also

- [Strategy interfaces reference](strategies.md)
- [Result pattern reference](result-pattern.md)
- [Model reference](model.md)
- [Canvas control reference](canvas-control.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
