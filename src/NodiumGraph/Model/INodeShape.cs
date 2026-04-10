using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Defines the geometric boundary of a node shape for port positioning.
/// Implementations compute where a ray at a given angle intersects the shape boundary.
/// </summary>
public interface INodeShape
{
    /// <summary>
    /// Returns the point on the shape boundary at the given angle, relative to the node center.
    /// </summary>
    /// <param name="angleDegrees">Angle in degrees. 0 = top, clockwise (90 = right, 180 = bottom, 270 = left).</param>
    /// <param name="width">Width of the node.</param>
    /// <param name="height">Height of the node.</param>
    /// <returns>Point relative to the node center (0,0).</returns>
    Point GetBoundaryPoint(double angleDegrees, double width, double height);
}
