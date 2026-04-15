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

/// <summary>
/// Cache entry for a connection's world-space rendered geometry. The
/// <see cref="Renderable"/> is reused across frames to skip re-routing and
/// re-building geometry; <see cref="Version"/> is a placeholder for Task 14
/// invalidation — assigned at insertion time and not yet consulted.
/// </summary>
internal readonly record struct CachedConnectionGeometry(
    ConnectionRenderable Renderable,
    int Version)
{
    public Rect WorldBounds => Renderable.WorldBounds;
}

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
    /// <para>
    /// Guard policy: the validity constraint for an inset depends on the route shape.
    /// For a 4-point bezier the actual curve length isn't cheap to compute, so we fall
    /// back to the straight-line distance between p0 and p3 as a conservative guard —
    /// this is a cheap approximation and may still skip insetting in rare degenerate
    /// control-point layouts, but never produces an invalid curve. For polyline/step
    /// routes the straight-line distance can be much shorter than the actual polyline
    /// length (e.g. a StepRouter zig-zag), which would wrongly skip insetting; the
    /// correct per-segment guard is that the source inset must not exceed the first
    /// segment's length and the target inset must not exceed the last segment's length.
    /// </para>
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

        if (kind == RouteKind.Bezier && points.Count == 4)
        {
            // Cheap conservative guard: the straight-line endpoint distance is a lower
            // bound a bezier curve may bow out from but a reasonable sanity check.
            var dx = pN.X - p0.X;
            var dy = pN.Y - p0.Y;
            var straight = System.Math.Sqrt(dx * dx + dy * dy);
            if (sourceInset + targetInset >= straight)
                return points;
        }
        else
        {
            // Polyline: the inset must not exceed its owning segment length. Using the
            // endpoint-to-endpoint distance here would wrongly reject a StepRouter
            // zig-zag whose actual polyline length is much larger than its AABB diagonal.
            var firstSeg = points[1] - points[0];
            var lastSeg = points[points.Count - 1] - points[points.Count - 2];
            var firstSegLength = System.Math.Sqrt(firstSeg.X * firstSeg.X + firstSeg.Y * firstSeg.Y);
            var lastSegLength = System.Math.Sqrt(lastSeg.X * lastSeg.X + lastSeg.Y * lastSeg.Y);
            if (sourceInset >= firstSegLength || targetInset >= lastSegLength)
                return points;
        }

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
    /// Convenience overload: routes the connection and builds the renderable in a single
    /// call. The canvas's per-frame loop prefers the
    /// <see cref="CreateRenderable(IReadOnlyList{Point}, RouteKind, IConnectionStyle)"/>
    /// overload with precomputed route points so it can route exactly once per connection
    /// per frame (routing for culling, then again for rendering would double the cost).
    /// </summary>
    public static ConnectionRenderable CreateRenderable(
        Connection connection, IConnectionRouter router, IConnectionStyle style)
    {
        var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
        return CreateRenderable(routePoints, router.RouteKind, style);
    }

    /// <summary>
    /// Builds the renderable split for a connection using precomputed route points.
    /// Prefer this overload when the caller already has a route in hand (e.g. the canvas
    /// connection loop uses the points for viewport culling before deciding to render).
    /// </summary>
    public static ConnectionRenderable CreateRenderable(
        IReadOnlyList<Point> routePoints, RouteKind routeKind, IConnectionStyle style)
    {
        if (routePoints.Count < 2)
        {
            return new ConnectionRenderable(
                new StreamGeometry(), null, null, default);
        }

        var tangents = RouteTangents.From(routePoints, routeKind);

        var sourceEndpoint = style.SourceEndpoint;
        var targetEndpoint = style.TargetEndpoint;
        var sourceInset = sourceEndpoint?.GetInset(style.Thickness) ?? 0;
        var targetInset = targetEndpoint?.GetInset(style.Thickness) ?? 0;

        var originalP0 = routePoints[0];
        var originalPN = routePoints[routePoints.Count - 1];

        var insetPoints = ApplyInsets(routePoints, tangents, sourceInset, targetInset, routeKind);
        var stroke = CreateStrokeGeometry(insetPoints, routeKind);

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
    /// context: optionally a halo under-pass, then one stroke draw, optionally one
    /// filled-endpoint draw, optionally one open-endpoint draw. Filled endpoints share
    /// the connection stroke brush for fill; open endpoints are stroke-only.
    /// <para>
    /// When <paramref name="selected"/> is <c>true</c> and <paramref name="haloPen"/>
    /// is non-null, a semi-transparent halo is drawn FIRST on the same three buckets
    /// (stroke, filled endpoints, open endpoints) using the wider halo pen — so the
    /// selection glow reads as a single unified shape around the stroke and its
    /// endpoint decorations. The normal passes are then drawn on top. When
    /// <paramref name="selected"/> is <c>false</c> OR <paramref name="haloPen"/> is
    /// null the halo pass is skipped and behavior is identical to the non-selected
    /// baseline. Filled-endpoint halo uses the halo brush as fill so the expanded
    /// silhouette around the shape reads as glow rather than as a hollow outline.
    /// </para>
    /// </summary>
    public static void Render(
        DrawingContext context,
        ConnectionRenderable renderable,
        IConnectionStyle style,
        Pen strokePen,
        bool selected,
        Pen? haloPen)
    {
        if (selected && haloPen is not null)
        {
            context.DrawGeometry(null, haloPen, renderable.Stroke);

            if (renderable.FilledEndpoints is not null)
                context.DrawGeometry(haloPen.Brush, haloPen, renderable.FilledEndpoints);

            if (renderable.OpenEndpoints is not null)
                context.DrawGeometry(null, haloPen, renderable.OpenEndpoints);
        }

        context.DrawGeometry(null, strokePen, renderable.Stroke);

        if (renderable.FilledEndpoints is not null)
            context.DrawGeometry(style.Stroke, strokePen, renderable.FilledEndpoints);

        if (renderable.OpenEndpoints is not null)
            context.DrawGeometry(null, strokePen, renderable.OpenEndpoints);
    }
}
