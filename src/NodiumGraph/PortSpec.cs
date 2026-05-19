using NodiumGraph.Model;

namespace NodiumGraph;

/// <summary>
/// Immutable snapshot of a single port declaration in <c>NodePortRegistry</c>.
/// Returned from <c>NodePortRegistry.TryGet</c> and consumed by <see cref="Model.Node"/>'s
/// lazy materializer.
/// </summary>
/// <remarks>
/// <see cref="Fraction"/> is nullable. <c>null</c> means "auto-layout" — the consumer
/// (<see cref="Model.Node.EnsureMaterialized"/>) constructs the port via the auto ctor,
/// and <c>FixedPortProvider</c> resolves the actual fraction at provider construction.
/// </remarks>
public readonly record struct PortSpec(
    string Name,
    PortFlow Flow,
    PortEdge Edge,
    double? Fraction,
    string? Label,
    uint? MaxConnections,
    object? DataType);
