using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as a cubic bezier curve with horizontally-offset control points.
/// Returns 4 points: start, control point 1, control point 2, end.
/// </summary>
public class BezierRouter : IConnectionRouter
{
    private const double MinOffset = 30.0;

    public bool IsBezierRoute => true;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var dx = Math.Abs(end.X - start.X);
        var offset = Math.Max(dx * 0.4, MinOffset);

        var cp1 = new Point(start.X + offset, start.Y);
        var cp2 = new Point(end.X - offset, end.Y);

        return [start, cp1, cp2, end];
    }
}
