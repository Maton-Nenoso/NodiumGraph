using Avalonia;

namespace NodiumGraph.Model;

internal static class GeometryHelpers
{
    public static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
