using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Default connection validator. Checks are applied in order:
/// identity (self-loop) → owner (same node) → flow (Out-to-Out, In-to-In) → <see cref="Port.DataType"/>.
/// Null DataType only matches null (strict) — wildcard-null would silently defeat type checks
/// during incremental adoption.
/// </summary>
public sealed class DefaultConnectionValidator : IConnectionValidator
{
    public static DefaultConnectionValidator Instance { get; } = new();

    public bool CanConnect(Port source, Port target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (ReferenceEquals(source, target)) return false;
        if (source.Owner == target.Owner) return false;
        if (source.Flow == target.Flow) return false;
        return Equals(source.DataType, target.DataType);
    }
}
