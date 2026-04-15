using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Sentinel endpoint renderer that draws nothing. Use when a connection end should have no decoration.
/// </summary>
public sealed class NoneEndpoint : IEndpointRenderer
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly NoneEndpoint Instance = new();

    private NoneEndpoint() { }

    public double GetInset(double strokeThickness) => 0;

    public bool IsFilled => false;

    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
        => new StreamGeometry();
}
