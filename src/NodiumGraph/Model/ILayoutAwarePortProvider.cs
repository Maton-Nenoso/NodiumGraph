namespace NodiumGraph.Model;

/// <summary>
/// A port provider that recalculates port positions when the node's layout (size/shape) changes.
/// The canvas calls UpdateLayout after measuring the node.
/// </summary>
public interface ILayoutAwarePortProvider : IPortProvider
{
    /// <summary>
    /// Called by the canvas when the node's measured size changes.
    /// Implementations should recompute all port positions.
    /// </summary>
    /// <param name="width">The node's measured width.</param>
    /// <param name="height">The node's measured height.</param>
    /// <param name="shape">The node's shape for boundary computation, or null for default rectangle.</param>
    void UpdateLayout(double width, double height, INodeShape? shape);

    /// <summary>
    /// Fired when port positions have been recomputed and the canvas should re-render.
    /// </summary>
    event Action? LayoutInvalidated;
}
