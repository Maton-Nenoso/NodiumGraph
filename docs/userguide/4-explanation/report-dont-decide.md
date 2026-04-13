# Report, Don't Decide

Every interaction in NodiumGraph obeys a single rule: **the canvas reports what the user did, it never decides what to do about it**. The user drags a node — the canvas tells you where the node ended up. The user drags a connection onto a port — the canvas asks whether you want to accept it. The user presses Delete — the canvas tells you which nodes and connections were selected. In every case the canvas stops at the boundary of "here is what happened" and leaves the decision of "what happens next" to your application.

This essay explains why the library draws that line, what it costs you, and what it unlocks.

## Two kinds of state

NodiumGraph works with two distinct kinds of state that happen to live side-by-side on screen:

- **Ephemeral UI state** — which node is being dragged right now, which port the pointer is hovering over, the viewport's current pan and zoom, the rubber-band rectangle mid-marquee. The canvas owns all of this. You cannot corrupt it because you cannot reach it from outside.
- **Persistent model state** — the set of `Node`s in the `Graph`, the `Connection`s between their ports, the per-node `X` / `Y` / `Title`, the domain data on your subclasses. You own all of this. The canvas never writes to it.

The rule makes this split rigorous. Every user gesture that would change persistent state (creating a node, accepting a connection, deleting, moving) is routed through one of the handler interfaces (`INodeInteractionHandler`, `IConnectionHandler`, `ISelectionHandler`, `ICanvasInteractionHandler`). The handler is a plain C# class *you* wrote. If you didn't wire a handler, the corresponding action has no persistent effect — the canvas observes it, then forgets it.

Handlers that do have real work to do are expected to call back into the model (`graph.AddConnection(...)`, `graph.RemoveNode(...)`, `node.X = ...`) themselves. There is no method on the canvas that secretly does it behind your back.

## A worked example: deleting a selection

Imagine the user selects three nodes and presses `Delete`. Here is what happens:

1. Your window's `KeyBinding` for `Delete` fires a command that calls `Canvas.DeleteSelected()`. (Both sides of that binding are yours — the library does not ship any keyboard shortcuts. See [the architecture essay](architecture.md).)
2. `Canvas.DeleteSelected()` collects the selected nodes and the connections that touch them into two lists, then calls `NodeHandler.OnDeleteRequested(nodes, connections)`. The canvas's own internal state is unchanged.
3. Your `NodeHandler` — you wrote it — receives the lists and decides what to do. Maybe it shows a confirm dialog. Maybe it checks whether any of the nodes are locked. Maybe it pushes an undo entry, then removes the nodes from the graph. Maybe it refuses entirely.
4. If your handler calls `graph.RemoveNode(node)`, the `Graph`'s `Nodes` collection raises a change notification. The canvas, watching `Nodes`, removes the node's `ContentControl` child from its visual tree and redraws. The visible state has caught up with the model.

Notice what didn't happen: the canvas never deleted anything on its own. It didn't even *start* to delete anything. It handed the list off and waited to see whether the model changed. Your code had full control over the semantics of "delete" — soft delete, hard delete, delete-with-confirm, delete-except-locked, compound undo entry — without the library needing to know any of it.

The same shape holds for every other mutation:

- **Accepting a new connection.** `OnConnectionRequested(source, target)` asks you for a `Result<Connection>`. Return success to accept, failure to reject. If you accept, you add the `Connection` to the graph yourself.
- **Moving a node.** `OnNodesMoved(moves)` fires after the drag completes, with the old and new positions for every affected node. You push the moves onto an undo stack; you do not set the positions, because the canvas already did that during the drag.
- **External drop.** `OnCanvasDropped(worldPosition, data)` hands you the drop location and the payload. The canvas has no opinion about what the payload means.

## Why the discipline

Because nobody's undo stack is the same.

Every app that grows past a certain size develops opinions about how state changes. It has a command object, an event source, a transactional boundary, a confirmation dialog, a validation layer, an audit log. If the canvas mutated the graph directly, *every one of those apps* would have to undo the library's guess and reapply its own convention, or fork the library to turn the mutation off. The library's guess is always wrong for someone.

By never deciding, the canvas stays out of that conversation. Your `RemoveNode` call is the same as any other call to `RemoveNode` — it runs under whatever transactional, validated, audit-logged discipline your app already uses, because it *is* your app making the call. The library becomes the piece of code between the user's pointer and your existing mutation pipeline, nothing more.

Undo / redo falls out of this almost for free. Because every persistent mutation runs through code you own, you can intercept each one and record it. `OnNodesMoved` gives you a ready-made "what changed, from what, to what" record. `OnDeleteRequested` gives you the full deletion payload before you commit it. The library ships no undo system, and it doesn't need to — the discipline makes it the user's job, and the user has the context to do it right.

## What it costs

The first ten minutes with NodiumGraph feel tedious. You wire up a handler to accept new connections, then another handler to delete them, then another to track moves. Each one is five to ten lines of boilerplate you'd rather not write, and each one could in principle have a default. The library's refusal to provide one seems like a gratuitous chore.

The refusal is the point. A library that provides a default mutation for `OnConnectionRequested` has *made a choice about your domain model*, quietly, in a codepath you didn't write. Five releases later, when that choice turns out to conflict with your own, you have to reach in and remove it. NodiumGraph would rather make you write the five lines of code yourself, while the semantics are still fresh in your head and the code is visible in your repo.

## In short

- The canvas owns transient UI state, the model owns persistent data, and the boundary between them is the handler interface layer.
- Every user action that could change the model is reported as a *request*, never performed as a *decision*.
- Your code decides whether, how, and under what discipline to mutate the graph in response.
- Undo, soft delete, confirmation, audit — all of it falls naturally onto your side of the line, where you already have the infrastructure for it.
- The small amount of wiring this requires is worth the fact that the library never fights your existing state-management conventions.
