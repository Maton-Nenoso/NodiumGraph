using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Strategy for providing and resolving ports on a node.
/// </summary>
public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position);
}
