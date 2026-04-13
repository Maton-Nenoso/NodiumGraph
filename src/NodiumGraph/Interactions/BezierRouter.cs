using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as a cubic bezier curve with horizontally-offset control points.
/// Returns 4 points: start, control point 1, control point 2, end.
/// </summary>
public class BezierRouter : IConnectionRouter
{
    public const double MinOffset = 30.0;
    public const double ControlOffsetFactor = 0.4;

    public static double ComputeControlOffset(double dx)
        => Math.Max(Math.Abs(dx) * ControlOffsetFactor, MinOffset);

    public RouteKind RouteKind => RouteKind.Bezier;

    public Rect GetLooseBounds(Port source, Port target)
    {
        var a = source.AbsolutePosition;
        var b = target.AbsolutePosition;
        // Control points preserve start.Y / end.Y and push only on X, so Y is the endpoint AABB.
        var offset = ComputeControlOffset(b.X - a.X);
        var minX = Math.Min(a.X, b.X) - offset;
        var maxX = Math.Max(a.X, b.X) + offset;
        var minY = Math.Min(a.Y, b.Y);
        var maxY = Math.Max(a.Y, b.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var dx = end.X - start.X;
        var offset = ComputeControlOffset(dx);

        // Push control points in the direction of travel.
        // Left-to-right (dx >= 0): cp1 right, cp2 left (toward each other).
        // Right-to-left (dx < 0): cp1 left, cp2 right (toward each other).
        var sign = dx >= 0 ? 1.0 : -1.0;
        var cp1 = new Point(start.X + offset * sign, start.Y);
        var cp2 = new Point(end.X - offset * sign, end.Y);

        return [start, cp1, cp2, end];
    }
}
