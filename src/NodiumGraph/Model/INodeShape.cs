using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Defines the geometric boundary of a node shape for port positioning.
/// Implementations compute the nearest point on the shape boundary to a given position
/// and provide the anchor↔boundary-point mapping that drives PortAnchor-based positioning.
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

    /// <summary>
    /// Returns the node-local (top-left origin) boundary point addressed by the anchor.
    /// Default implementation throws — shapes must override.
    /// </summary>
    Point GetEdgePoint(PortAnchor anchor, double width, double height)
        => throw new NotSupportedException($"{GetType().Name} does not implement GetEdgePoint.");

    /// <summary>
    /// Returns the outward unit normal at the boundary point addressed by the anchor.
    /// Default implementation throws — shapes must override.
    /// </summary>
    Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height)
        => throw new NotSupportedException($"{GetType().Name} does not implement GetEdgeOutwardNormal.");

    /// <summary>
    /// Returns the canonical anchor whose <see cref="GetEdgePoint"/> matches the given boundary point.
    /// Default implementation throws — shapes must override.
    /// </summary>
    PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
        => throw new NotSupportedException($"{GetType().Name} does not implement InferAnchor.");
}
