using NodiumGraph.Model;
using Avalonia;

namespace NodiumGraph.Interactions;

/// <summary>
/// Computes the visual path between two connected ports (bezier, orthogonal, etc.).
/// </summary>
public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
    RouteKind RouteKind { get; }
}
