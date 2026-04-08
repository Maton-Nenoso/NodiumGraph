using Avalonia;

namespace NodiumGraph.Model;

public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
