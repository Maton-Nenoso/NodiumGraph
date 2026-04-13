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
    private const double ControlOffsetFactor = 0.4;

    public RouteKind RouteKind => RouteKind.Bezier;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var dx = end.X - start.X;
        var offset = Math.Max(Math.Abs(dx) * ControlOffsetFactor, MinOffset);

        // Push control points in the direction of travel.
        // Left-to-right (dx >= 0): cp1 right, cp2 left (toward each other).
        // Right-to-left (dx < 0): cp1 left, cp2 right (toward each other).
        var sign = dx >= 0 ? 1.0 : -1.0;
        var cp1 = new Point(start.X + offset * sign, start.Y);
        var cp2 = new Point(end.X - offset * sign, end.Y);

        return [start, cp1, cp2, end];
    }
}
