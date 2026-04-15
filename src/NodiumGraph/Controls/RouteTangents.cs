using System.Collections.Generic;
using Avalonia;
using NodiumGraph.Interactions;

namespace NodiumGraph.Controls;

/// <summary>
/// Source and target tangent vectors of a routed connection, used to orient
/// endpoint decorations along the connection's direction of travel at each end.
/// </summary>
internal readonly record struct RouteTangents(Vector Source, Vector Target)
{
    private const double MinLength = 1e-9;

    /// <summary>
    /// Computes the source and target tangents for a routed point list.
    /// </summary>
    public static RouteTangents From(IReadOnlyList<Point> points, RouteKind kind)
    {
        if (points is null || points.Count < 2)
        {
            return default;
        }

        if (kind == RouteKind.Bezier && points.Count == 4)
        {
            var source = Normalize(points[1] - points[0]);
            var target = Normalize(points[3] - points[2]);
            return new RouteTangents(source, target);
        }

        var first = Normalize(points[1] - points[0]);
        var last = Normalize(points[points.Count - 1] - points[points.Count - 2]);
        return new RouteTangents(first, last);
    }

    private static Vector Normalize(Vector v)
    {
        var length = v.Length;
        if (length < MinLength)
        {
            return default;
        }

        return new Vector(v.X / length, v.Y / length);
    }
}
