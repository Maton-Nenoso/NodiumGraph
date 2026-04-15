using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// World-space hit-testing over cached connection geometry. Iterates connections in
/// reverse z-order (last in the list is topmost) and returns the first whose cached
/// renderable geometry contains the query point within <paramref name="worldTolerance"/>.
/// </summary>
internal static class ConnectionHitTester
{
    /// <summary>
    /// Returns the topmost connection whose cached geometry contains the given world
    /// point, or <c>null</c> if no connection is hit. Iterates in reverse z-order
    /// (last in list = topmost). Connections missing from <paramref name="cache"/>
    /// are skipped silently.
    /// </summary>
    public static Connection? HitTest(
        Point worldPoint,
        double worldTolerance,
        IReadOnlyList<Connection> connections,
        Func<Connection, IConnectionStyle> resolveStyle,
        IReadOnlyDictionary<Guid, CachedConnectionGeometry> cache)
    {
        for (var i = connections.Count - 1; i >= 0; i--)
        {
            var connection = connections[i];
            if (!cache.TryGetValue(connection.Id, out var entry))
                continue;

            // Fast reject: an inflated world-AABB test skips the more expensive
            // stroke/fill check for any connection clearly away from the cursor.
            if (!entry.WorldBounds.Inflate(worldTolerance).Contains(worldPoint))
                continue;

            var style = resolveStyle(connection);
            var hitThickness = Math.Max(style.Thickness, worldTolerance);
            // StrokeContains ignores brush; reuse the framework singleton to avoid
            // per-click allocation of a SolidColorBrush.
            var hitPen = new Pen(Brushes.Black, hitThickness);

            var hitShape = BuildHitTestShape(entry.Renderable);
            if (hitShape.StrokeContains(hitPen, worldPoint) ||
                hitShape.FillContains(worldPoint))
            {
                return connection;
            }
        }

        return null;
    }

    /// <summary>
    /// Unions stroke + filled + open endpoint geometries into a single
    /// <see cref="GeometryGroup"/> so a single StrokeContains/FillContains pair
    /// covers the full connection silhouette. In the common no-endpoint case the
    /// stroke is returned directly to avoid the wrapper allocation.
    /// </summary>
    private static Geometry BuildHitTestShape(ConnectionRenderable renderable)
    {
        if (renderable.FilledEndpoints is null && renderable.OpenEndpoints is null)
            return renderable.Stroke;

        var group = new GeometryGroup { FillRule = FillRule.NonZero };
        group.Children.Add(renderable.Stroke);
        if (renderable.FilledEndpoints is not null)
            group.Children.Add(renderable.FilledEndpoints);
        if (renderable.OpenEndpoints is not null)
            group.Children.Add(renderable.OpenEndpoints);
        return group;
    }
}
