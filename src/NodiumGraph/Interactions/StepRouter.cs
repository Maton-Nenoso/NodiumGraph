using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as an orthogonal (Manhattan) path with horizontal and vertical segments.
/// </summary>
public class StepRouter : IConnectionRouter
{
    public RouteKind RouteKind => RouteKind.Polyline;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        if (Math.Abs(start.Y - end.Y) < 0.001)
            return [start, end];

        if (Math.Abs(start.X - end.X) < 0.001)
            return [start, end];

        var midX = (start.X + end.X) / 2;

        return
        [
            start,
            new Point(midX, start.Y),
            new Point(midX, end.Y),
            end
        ];
    }
}
