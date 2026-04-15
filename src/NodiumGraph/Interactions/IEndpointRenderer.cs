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
    /// </summary>
    /// <param name="tip">The tip point where the decoration meets the port (world space).</param>
    /// <param name="direction">Unit vector pointing outward from the connection toward the tip.</param>
    /// <param name="strokeThickness">The connection's stroke thickness, for size-proportional decorations.</param>
    Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness);
}
