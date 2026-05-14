using NodiumGraph.Model;

namespace NodiumGraph;

/// <summary>
/// Immutable snapshot of a single port declaration in <c>NodePortRegistry</c>.
/// Returned from <c>NodePortRegistry.TryGet</c> and consumed by <see cref="Model.Node"/>'s
/// lazy materializer.
/// </summary>
public readonly record struct PortSpec(
    string Name,
    PortFlow Flow,
    PortEdge Edge,
    double Fraction,
    string? Label,
    uint? MaxConnections,
    object? DataType);
