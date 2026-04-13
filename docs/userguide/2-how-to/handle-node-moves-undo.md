# Handle Node Moves for Undo/Redo

## Goal

Capture the before and after positions of every user-initiated node drag so you can push them onto an undo stack — and redo them later without the canvas fighting you.

## Prerequisites

- You already host `NodiumGraphCanvas` and have a `Graph` assigned. If not, see [Host the Canvas](host-canvas.md).
- You understand why NodiumGraph [reports, doesn't decide](../4-explanation/report-dont-decide.md) — the canvas never mutates `Node.X` / `Node.Y` behind your back.

## Steps

### 1. Why a dedicated handler matters

During a drag, the canvas updates each affected `Node.X` / `Node.Y` continuously so the user sees the node follow the pointer. But it also remembers each node's starting position. When the drag completes, the canvas calls `INodeInteractionHandler.OnNodesMoved` **once** — not once per pointer-move — with the list of `NodeMoveInfo` records. That list is exactly what an undo stack wants: a node, an old position, a new position.

You don't get any `OnNodesMoving` callback during the drag. That's intentional — pushing one undo entry per frame would fill the stack in seconds.

### 2. Shape of the move record

```csharp
// from: src/NodiumGraph/Model/NodeMoveInfo.cs
public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
```

Positions are in world units. Because `NodeMoveInfo` is a `record`, it has value equality and is safe to retain — but see the defensive-copy note below for the containing list.

### 3. Implement `INodeInteractionHandler.OnNodesMoved`

```csharp
public sealed class NodeUndoHandler(UndoStack undo) : INodeInteractionHandler
{
    public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves)
    {
        // Defensive copy — the library may reuse the backing list on a subsequent drag.
        var snapshot = moves.ToList();
        undo.Push(new MoveNodesOp(snapshot));
    }

    public void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections)
    {
        // unrelated to this recipe, but OnDeleteRequested must exist on the interface
    }

    public void OnNodeDoubleClicked(Node node)
    {
        // likewise
    }
}
```

And wire it on the canvas:

```csharp
Canvas.NodeHandler = new NodeUndoHandler(_undo);
```

### 4. Implement the undo operation

Undo and redo both just write to `Node.X` / `Node.Y` — the `Graph` fires its own property-changed notifications, and the canvas repositions the visuals and reroutes any connections touching the moved nodes. Keep the operation minimal:

```csharp
public sealed class MoveNodesOp(IReadOnlyList<NodeMoveInfo> moves) : IUndoOp
{
    public void Undo()
    {
        foreach (var m in moves)
        {
            m.Node.X = m.OldPosition.X;
            m.Node.Y = m.OldPosition.Y;
        }
    }

    public void Redo()
    {
        foreach (var m in moves)
        {
            m.Node.X = m.NewPosition.X;
            m.Node.Y = m.NewPosition.Y;
        }
    }
}
```

`UndoStack` and `IUndoOp` are whatever you already use — NodiumGraph does not prescribe an undo framework.

### 5. Keyboard wiring

Undo/redo shortcuts are outside NodiumGraph's scope — it's the consumer's responsibility. Bind `Ctrl+Z` / `Ctrl+Y` in your window:

```xml
<Window.KeyBindings>
  <KeyBinding Gesture="Ctrl+Z" Command="{Binding UndoCommand}" />
  <KeyBinding Gesture="Ctrl+Y" Command="{Binding RedoCommand}" />
</Window.KeyBindings>
```

## Full code

```csharp
public sealed class NodeUndoHandler(UndoStack undo) : INodeInteractionHandler
{
    public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves)
    {
        undo.Push(new MoveNodesOp(moves.ToList()));
    }

    public void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections)
    {
        foreach (var connection in connections)
            undo.Graph.RemoveConnection(connection);
        foreach (var node in nodes)
            undo.Graph.RemoveNode(node);
        undo.Push(new DeleteOp(nodes.ToList(), connections.ToList()));
    }

    public void OnNodeDoubleClicked(Node node)
    {
        // e.g. open an inspector
    }
}

public sealed class MoveNodesOp(IReadOnlyList<NodeMoveInfo> moves) : IUndoOp
{
    public void Undo()
    {
        foreach (var m in moves)
        {
            m.Node.X = m.OldPosition.X;
            m.Node.Y = m.OldPosition.Y;
        }
    }

    public void Redo()
    {
        foreach (var m in moves)
        {
            m.Node.X = m.NewPosition.X;
            m.Node.Y = m.NewPosition.Y;
        }
    }
}
```

## Gotchas

- **Defensive-copy the move list.** The library documents that the `IReadOnlyList<NodeMoveInfo>` handed to you may be reused on a later drag. Call `.ToList()` — or project into your own record — if you store it past the end of `OnNodesMoved`. This is the same contract as `ISelectionHandler.OnSelectionChanged`.
- **Do not call back into `Graph` to re-move the nodes.** The canvas has already written the new positions by the time `OnNodesMoved` fires. Pushing the record onto your undo stack is the entire job; the model is already consistent.
- **`OnNodesMoved` covers multi-selection drags.** If the user drags a selected group, the list contains every node in the group — one record each. Treat the whole list as a single atomic undo entry so a single `Ctrl+Z` reverts the whole group motion.
- **Snap-to-grid is already applied.** When `SnapToGrid` is `true` on the canvas, `NewPosition` is the snapped position, not the raw drag end. You don't need to re-snap in your handler.
- **Cancelling a drag does not call the handler.** If the user presses Escape or releases outside the canvas in a way the canvas treats as cancel, `OnNodesMoved` does not fire for that drag. You won't get a phantom undo entry.
- **Programmatic `Node.X = ...` writes bypass the handler.** If your own code (loader, layout pass, undo/redo itself) writes positions, it does not go through `OnNodesMoved`. Treat the handler as a "user drag completed" event, not as a general "node moved" event.

## See also

- [Handler interfaces reference](../3-reference/handlers.md)
- [Model reference](../3-reference/model.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
- [Snap to grid](snap-to-grid.md)
- [Getting Started tutorial](../1-tutorial/getting-started.md)
