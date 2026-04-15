using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Per-connection visual style (stroke, thickness, dash pattern).
/// </summary>
public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }

    /// <summary>Endpoint renderer drawn at the source end of the connection, or null for none.</summary>
    IEndpointRenderer? SourceEndpoint { get; }

    /// <summary>Endpoint renderer drawn at the target end of the connection, or null for none.</summary>
    IEndpointRenderer? TargetEndpoint { get; }
}
