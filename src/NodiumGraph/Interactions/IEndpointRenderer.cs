using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Strategy for rendering a decoration at one end of a connection (e.g. arrow, diamond, circle).
/// Implementations describe how much the connection path should be shortened to make room for the
/// decoration and how the decoration geometry is built for a given tip point and emission direction.
/// </summary>
public interface IEndpointRenderer
{
    /// <summary>
    /// Returns how far along the emission direction the connection path should be inset
    /// so that its stroke does not overlap the endpoint decoration.
    /// </summary>
    double GetInset(double strokeThickness);

    /// <summary>
    /// True when the endpoint geometry should be rendered as a solid fill; false for stroke-only.
    /// </summary>
    bool IsFilled { get; }

    /// <summary>
    /// Builds the geometry for the endpoint decoration.
    /// <paramref name="tip"/> is the port center in world coordinates.
    /// <paramref name="direction"/> is a unit vector pointing from the connection line outward along the
    /// curve tangent at this endpoint; implementations may assume <c>|direction| ≈ 1</c>.
    /// </summary>
    /// <param name="tip">The port center in world coordinates.</param>
    /// <param name="direction">
    /// Unit vector pointing from the connection line outward, along the curve tangent at this endpoint.
    /// Implementations may assume <c>|direction| ≈ 1</c>.
    /// </param>
    /// <param name="strokeThickness">The connection's stroke thickness, for size-proportional decorations.</param>
    Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness);
}
