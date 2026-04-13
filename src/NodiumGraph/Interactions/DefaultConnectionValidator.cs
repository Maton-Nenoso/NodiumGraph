using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Default connection validator. Rejects self-connections, same-owner connections,
/// same-flow pairs (Out-to-Out, In-to-In), and mismatched Port.DataType values.
/// Null DataType only matches null (strict) — wildcard-null would silently defeat type checks
/// during incremental adoption.
/// </summary>
public sealed class DefaultConnectionValidator : IConnectionValidator
{
    public static DefaultConnectionValidator Instance { get; } = new();

    public bool CanConnect(Port source, Port target)
    {
        if (ReferenceEquals(source, target)) return false;
        if (source.Owner == target.Owner) return false;
        if (source.Flow == target.Flow) return false;
        return Equals(source.DataType, target.DataType);
    }
}
