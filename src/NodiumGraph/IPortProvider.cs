using Avalonia;

namespace NodiumGraph;

public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position);
}
