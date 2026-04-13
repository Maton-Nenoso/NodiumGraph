using NodiumGraph.Model;
using Avalonia;

namespace NodiumGraph.Interactions;

/// <summary>
/// Computes the visual path between two connected ports (bezier, orthogonal, etc.).
/// </summary>
public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
    RouteKind RouteKind { get; }

    /// <summary>
    /// World-space AABB that contains every point <see cref="Route"/> would produce for this
    /// endpoint pair, loose enough to use for viewport culling. Stroke bleed is added by the
    /// caller; implementations only need to cover route excursion (e.g. bezier control points).
    /// </summary>
    Rect GetLooseBounds(Port source, Port target)
    {
        var a = source.AbsolutePosition;
        var b = target.AbsolutePosition;
        var minX = Math.Min(a.X, b.X);
        var minY = Math.Min(a.Y, b.Y);
        return new Rect(minX, minY, Math.Max(a.X, b.X) - minX, Math.Max(a.Y, b.Y) - minY);
    }
}
