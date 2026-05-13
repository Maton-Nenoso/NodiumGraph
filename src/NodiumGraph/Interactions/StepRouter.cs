using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as an orthogonal (Manhattan) path. Leg orientations are chosen
/// from each port's emission direction (derived from which edge of the owner node the
/// port sits on). Two horizontally-emitting ports produce an H–V–H path through midX;
/// two vertically-emitting ports produce a V–H–V path through midY; a mixed pair
/// produces a single L-bend whose corner matches both emissions.
/// </summary>
public class StepRouter : IConnectionRouter
{
    private const double Epsilon = 0.001;

    public RouteKind RouteKind => RouteKind.Polyline;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var sourceDir = source.EmissionDirection;
        var targetDir = target.EmissionDirection;

        var sourceHorizontal = Math.Abs(sourceDir.X) > 0.5;
        var targetHorizontal = Math.Abs(targetDir.X) > 0.5;

        Point[] raw;

        if (sourceHorizontal && targetHorizontal)
        {
            if (Math.Abs(start.Y - end.Y) < Epsilon)
                return [start, end];

            var midX = (start.X + end.X) / 2;
            raw = [start, new Point(midX, start.Y), new Point(midX, end.Y), end];
        }
        else if (!sourceHorizontal && !targetHorizontal)
        {
            if (Math.Abs(start.X - end.X) < Epsilon)
                return [start, end];

            var midY = (start.Y + end.Y) / 2;
            raw = [start, new Point(start.X, midY), new Point(end.X, midY), end];
        }
        else if (sourceHorizontal)
        {
            raw = [start, new Point(end.X, start.Y), end];
        }
        else
        {
            raw = [start, new Point(start.X, end.Y), end];
        }

        return Dedup(raw);
    }

    private static IReadOnlyList<Point> Dedup(Point[] points)
    {
        var result = new List<Point>(points.Length) { points[0] };
        for (int i = 1; i < points.Length; i++)
        {
            var prev = result[^1];
            if (Math.Abs(prev.X - points[i].X) < Epsilon && Math.Abs(prev.Y - points[i].Y) < Epsilon)
                continue;
            result.Add(points[i]);
        }
        return result;
    }
}
