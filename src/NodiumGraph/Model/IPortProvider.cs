using Avalonia;

namespace NodiumGraph.Model;

public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position);
}
