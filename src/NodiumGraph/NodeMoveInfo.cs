using Avalonia;

namespace NodiumGraph;

public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
