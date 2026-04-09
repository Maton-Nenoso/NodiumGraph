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
}
