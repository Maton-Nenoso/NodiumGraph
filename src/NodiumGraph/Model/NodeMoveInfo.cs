using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Snapshot of a node's position before and after a drag operation.
/// The <see cref="Node"/> reference may outlive its presence in the graph —
/// consumers using this for undo/redo must handle the case where the node has been removed.
/// </summary>
public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
