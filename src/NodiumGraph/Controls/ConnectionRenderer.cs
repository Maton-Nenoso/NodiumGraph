using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// A connection's rendered geometry split into three buckets so <see cref="ConnectionRenderer.Render"/>
/// can issue at most three <see cref="DrawingContext.DrawGeometry"/> calls per connection — one for the
/// (possibly inset) stroke, one for filled endpoint decorations, one for open (stroke-only) decorations.
/// </summary>
internal readonly record struct ConnectionRenderable(
    Geometry Stroke,
    Geometry? FilledEndpoints,
    Geometry? OpenEndpoints,
    Rect WorldBounds);

internal static class ConnectionRenderer
{
    /// <summary>
    /// Builds the stroke geometry on a possibly-inset point list.
    /// </summary>
    private static Geometry CreateStrokeGeometry(
        IReadOnlyList<Point> routePoints, RouteKind routeKind)
    {
        if (routePoints.Count < 2)
            return new StreamGeometry();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(routePoints[0], false);

            if (routeKind == RouteKind.Bezier && routePoints.Count == 4)
            {
                ctx.CubicBezierTo(routePoints[1], routePoints[2], routePoints[3]);
            }
            else
            {
                for (var i = 1; i < routePoints.Count; i++)
                    ctx.LineTo(routePoints[i]);
            }

            ctx.EndFigure(false);
        }

        return geo;
    }

    /// <summary>
    /// Applies source/target endpoint insets to a routed point list.
    /// For bezier routes (4 points) only the endpoints p0 and p3 move — the control
    /// points stay put so the curve still leaves/enters tangentially at the same angle.
    /// For polylines only p0 and p_N move along the first/last-segment directions.
    /// If the combined inset would exceed the straight-line distance between p0 and p_N,
    /// insetting is skipped entirely (returns the original list).
    /// </summary>
    private static IReadOnlyList<Point> ApplyInsets(
        IReadOnlyList<Point> points,
        RouteTangents tangents,
        double sourceInset,
        double targetInset,
        RouteKind kind)
    {
        if (points.Count < 2 || (sourceInset <= 0 && targetInset <= 0))
            return points;

        var p0 = points[0];
        var pN = points[points.Count - 1];
        var dx = pN.X - p0.X;
        var dy = pN.Y - p0.Y;
        var distance = System.Math.Sqrt(dx * dx + dy * dy);
        if (sourceInset + targetInset >= distance)
            return points;

        // Source tangent is curve velocity pointing INTO the curve (away from p0).
        // Moving p0 in that direction shortens the first segment.
        var applySource = sourceInset > 0 && tangents.Source != default;
        // Target tangent is curve velocity pointing INTO the target port. Subtracting
        // it from pN walks back along the curve.
        var applyTarget = targetInset > 0 && tangents.Target != default;

        if (!applySource && !applyTarget)
            return points;

        var newP0 = applySource ? p0 + tangents.Source * sourceInset : p0;
        var newPN = applyTarget ? pN - tangents.Target * targetInset : pN;

        if (kind == RouteKind.Bezier && points.Count == 4)
        {
            return new[] { newP0, points[1], points[2], newPN };
        }

        // Polyline: only replace p0 and p_N, keep interior points intact.
        var result = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
            result[i] = points[i];
        result[0] = newP0;
        result[result.Length - 1] = newPN;
        return result;
    }

    /// <summary>
    /// Builds the renderable split for a connection: stroke + bucketed endpoint groups.
    /// </summary>
    public static ConnectionRenderable CreateRenderable(
        Connection connection, IConnectionRouter router, IConnectionStyle style)
    {
        var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
        if (routePoints.Count < 2)
        {
            return new ConnectionRenderable(
                new StreamGeometry(), null, null, default);
        }

        var tangents = RouteTangents.From(routePoints, router.RouteKind);

        var sourceEndpoint = style.SourceEndpoint;
        var targetEndpoint = style.TargetEndpoint;
        var sourceInset = sourceEndpoint?.GetInset(style.Thickness) ?? 0;
        var targetInset = targetEndpoint?.GetInset(style.Thickness) ?? 0;

        var originalP0 = routePoints[0];
        var originalPN = routePoints[routePoints.Count - 1];

        var insetPoints = ApplyInsets(routePoints, tangents, sourceInset, targetInset, router.RouteKind);
        var stroke = CreateStrokeGeometry(insetPoints, router.RouteKind);

        GeometryGroup? filledGroup = null;
        GeometryGroup? openGroup = null;

        // Source end: BuildGeometry wants the outward direction (toward the source port,
        // away from the curve body). tangents.Source points into the curve, so negate it.
        if (sourceEndpoint is not null && tangents.Source != default)
        {
            var geo = sourceEndpoint.BuildGeometry(originalP0, -tangents.Source, style.Thickness);
            AddToBucket(ref filledGroup, ref openGroup, geo, sourceEndpoint.IsFilled);
        }

        // Target end: tangents.Target is curve velocity pointing INTO the target port,
        // which is already the outward direction for the target decoration.
        if (targetEndpoint is not null && tangents.Target != default)
        {
            var geo = targetEndpoint.BuildGeometry(originalPN, tangents.Target, style.Thickness);
            AddToBucket(ref filledGroup, ref openGroup, geo, targetEndpoint.IsFilled);
        }

        var worldBounds = ComputeWorldBounds(stroke, filledGroup, openGroup);

        return new ConnectionRenderable(stroke, filledGroup, openGroup, worldBounds);
    }

    private static void AddToBucket(
        ref GeometryGroup? filledGroup,
        ref GeometryGroup? openGroup,
        Geometry geometry,
        bool isFilled)
    {
        if (isFilled)
        {
            filledGroup ??= new GeometryGroup();
            filledGroup.Children.Add(geometry);
        }
        else
        {
            openGroup ??= new GeometryGroup();
            openGroup.Children.Add(geometry);
        }
    }

    private static Rect ComputeWorldBounds(
        Geometry stroke, Geometry? filled, Geometry? open)
    {
        var bounds = stroke.Bounds;
        if (filled is not null)
            bounds = bounds.Union(filled.Bounds);
        if (open is not null)
            bounds = bounds.Union(open.Bounds);
        return bounds;
    }

    /// <summary>
    /// Builds the combined geometry (stroke + endpoints) as a <see cref="GeometryGroup"/>.
    /// Used by callers that need a single geometry blob (e.g. hit-testing or bounds
    /// computation in tests). Per-bucket rendering should prefer
    /// <see cref="CreateRenderable"/> which avoids allocating wrapper groups for the
    /// common no-endpoint case.
    /// </summary>
    public static Geometry CreateGeometry(
        Connection connection, IConnectionRouter router, IConnectionStyle style)
    {
        var renderable = CreateRenderable(connection, router, style);
        if (renderable.FilledEndpoints is null && renderable.OpenEndpoints is null)
            return renderable.Stroke;

        var group = new GeometryGroup();
        group.Children.Add(renderable.Stroke);
        if (renderable.FilledEndpoints is not null)
            group.Children.Add(renderable.FilledEndpoints);
        if (renderable.OpenEndpoints is not null)
            group.Children.Add(renderable.OpenEndpoints);
        return group;
    }

    /// <summary>
    /// Renders a previously-built <see cref="ConnectionRenderable"/> to the drawing
    /// context: one stroke draw, optionally one filled-endpoint draw, optionally one
    /// open-endpoint draw. Filled endpoints share the connection stroke brush for fill;
    /// open endpoints are stroke-only.
    /// </summary>
    public static void Render(
        DrawingContext context,
        ConnectionRenderable renderable,
        IConnectionStyle style,
        Pen strokePen)
    {
        context.DrawGeometry(null, strokePen, renderable.Stroke);

        if (renderable.FilledEndpoints is not null)
            context.DrawGeometry(style.Stroke, strokePen, renderable.FilledEndpoints);

        if (renderable.OpenEndpoints is not null)
            context.DrawGeometry(null, strokePen, renderable.OpenEndpoints);
    }
}
