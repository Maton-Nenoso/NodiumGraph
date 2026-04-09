namespace NodiumGraph;

/// <summary>
/// Describes a failure with a human-readable message and an optional machine-readable code.
/// </summary>
public record Error(string Message, string? Code = null);
