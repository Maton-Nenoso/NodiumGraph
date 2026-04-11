using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Provides ports for a node and resolves hit-test positions to port instances.
/// Set per node instance to control how ports are created, positioned, and resolved.
/// </summary>
public interface IPortProvider
{
    /// <summary>
    /// The current set of ports owned by this provider.
    /// </summary>
    IReadOnlyList<Port> Ports { get; }

    /// <summary>
    /// Resolves a canvas-relative position to the nearest port within the provider's hit-test radius.
    /// When <paramref name="preview"/> is true, the result is tentative (e.g., for hover feedback)
    /// and must not create permanent state. When false, the resolve is a commit and may create ports.
    /// </summary>
    /// <param name="position">Hit-test position in node-local coordinates.</param>
    /// <param name="preview">True for hover/preview resolve, false for commit.</param>
    /// <returns>The matched port, or null if no port is within range.</returns>
    Port? ResolvePort(Point position, bool preview);

    /// <summary>
    /// Cancels the most recent uncommitted resolve, rolling back any tentative state
    /// created during a preview resolve (e.g., removing a provisionally created dynamic port).
    /// </summary>
    void CancelResolve();

    /// <summary>Raised when a port is added to <see cref="Ports"/>.</summary>
    event Action<Port>? PortAdded;

    /// <summary>Raised when a port is removed from <see cref="Ports"/>.</summary>
    event Action<Port>? PortRemoved;
}
