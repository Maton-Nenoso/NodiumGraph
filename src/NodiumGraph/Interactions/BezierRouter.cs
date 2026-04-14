using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as a cubic bezier curve. Control points are pushed along each port's
/// outward emission direction, derived from which edge of the owner node the port sits on.
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

        var sourceDir = GetEmissionDirection(source);
        var targetDir = GetEmissionDirection(target);

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        var sourceReach = Math.Abs(dx * sourceDir.X + dy * sourceDir.Y);
        var targetReach = Math.Abs(dx * targetDir.X + dy * targetDir.Y);

        var sourceOffset = Math.Max(sourceReach * ControlOffsetFactor, MinOffset);
        var targetOffset = Math.Max(targetReach * ControlOffsetFactor, MinOffset);

        var cp1 = new Point(start.X + sourceDir.X * sourceOffset,
                            start.Y + sourceDir.Y * sourceOffset);
        var cp2 = new Point(end.X + targetDir.X * targetOffset,
                            end.Y + targetDir.Y * targetOffset);

        return [start, cp1, cp2, end];
    }

    private static Vector GetEmissionDirection(Port port)
    {
        var owner = port.Owner;
        var px = port.Position.X;
        var py = port.Position.Y;

        var leftDist = px;
        var rightDist = owner.Width - px;
        var topDist = py;
        var bottomDist = owner.Height - py;

        // Smallest signed distance wins. Negative means the port is outside on that side —
        // correctly picked. Ties between horizontal and vertical break toward horizontal to
        // preserve today's behavior for corner / interior / zero-size ports.
        var minHorizontal = Math.Min(leftDist, rightDist);
        var minVertical = Math.Min(topDist, bottomDist);

        if (minHorizontal <= minVertical)
        {
            return leftDist <= rightDist ? new Vector(-1, 0) : new Vector(1, 0);
        }

        return topDist <= bottomDist ? new Vector(0, -1) : new Vector(0, 1);
    }
}
