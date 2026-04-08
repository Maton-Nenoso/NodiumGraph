using NodiumGraph.Model;
using Avalonia;

namespace NodiumGraph.Interactions;

public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
}
