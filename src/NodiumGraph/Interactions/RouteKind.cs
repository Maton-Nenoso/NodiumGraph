namespace NodiumGraph.Interactions;

/// <summary>
/// Describes the type of path segments returned by an <see cref="IConnectionRouter"/>.
/// </summary>
public enum RouteKind
{
    /// <summary>Straight-line segments (polyline).</summary>
    Polyline,

    /// <summary>Cubic bezier curve (exactly 4 control points).</summary>
    Bezier
}
