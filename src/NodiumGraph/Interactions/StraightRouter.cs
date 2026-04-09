using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as a straight line between source and target ports.
/// </summary>
public class StraightRouter : IConnectionRouter
{
    public IReadOnlyList<Point> Route(Port source, Port target) =>
        [source.AbsolutePosition, target.AbsolutePosition];
}
