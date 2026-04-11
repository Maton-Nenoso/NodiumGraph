using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Defines the geometric boundary of a node shape for port positioning.
/// Implementations compute the nearest point on the shape boundary to a given position.
/// </summary>
public interface INodeShape
{
    /// <summary>
    /// Returns the nearest point on the shape boundary to the given position.
    /// </summary>
    /// <param name="position">Center-relative coordinates (0,0 = node center).</param>
    /// <param name="width">Width of the node.</param>
    /// <param name="height">Height of the node.</param>
    /// <returns>Center-relative point on the boundary.</returns>
    Point GetNearestBoundaryPoint(Point position, double width, double height);
}
