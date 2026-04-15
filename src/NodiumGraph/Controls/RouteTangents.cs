using System.Collections.Generic;
using Avalonia;
using NodiumGraph.Interactions;

namespace NodiumGraph.Controls;

/// <summary>
/// Source and target tangent vectors of a routed connection. Both vectors are
/// expressed as <b>curve velocity in the source-to-target direction</b>:
/// <list type="bullet">
///   <item><description>
///     <see cref="Source"/> is the outgoing velocity at the source port — it points
///     <i>from</i> the source port <i>into</i> the curve body.
///   </description></item>
///   <item><description>
///     <see cref="Target"/> is the incoming velocity at the target port — it points
///     <i>from</i> the curve body <i>into</i> the target port.
///   </description></item>
/// </list>
/// A renderer placing a decoration at the <i>source</i> end (pointing "outward" away
/// from the curve, toward the source port) must <b>negate <see cref="Source"/></b>.
/// A renderer placing a decoration at the <i>target</i> end (pointing "outward" away
/// from the curve, toward the target port — i.e. along curve velocity at p3) uses
/// <see cref="Target"/> directly.
/// </summary>
internal readonly record struct RouteTangents(Vector Source, Vector Target)
{
    private const double MinLength = 1e-9;

    /// <summary>
    /// Computes the source and target tangents for a routed point list.
    /// <para>
    /// Returns <c>default</c> (both tangents zero) when <paramref name="points"/> is
    /// <see langword="null"/> or has fewer than 2 elements. Returns zero for an
    /// individual tangent when its defining segment has length &lt; 1e-9, so callers
    /// should check <c>tangent != default</c> before using it.
    /// </para>
    /// <para>
    /// When <paramref name="kind"/> is <see cref="RouteKind.Bezier"/> but
    /// <paramref name="points"/> does not contain exactly 4 elements, the method
    /// falls back to the polyline first/last-segment rule. This is intentional
    /// defensive behavior — a well-behaved <c>BezierRouter</c> always returns exactly
    /// 4 points (p0, cp1, cp2, p3), but the fallback keeps the helper useful if a
    /// custom router ever returns more or fewer.
    /// </para>
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
