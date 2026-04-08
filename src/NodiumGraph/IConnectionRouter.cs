using Avalonia;

namespace NodiumGraph;

public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
}
