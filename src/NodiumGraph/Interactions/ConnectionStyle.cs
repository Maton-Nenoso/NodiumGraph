using Avalonia.Media;

namespace NodiumGraph.Interactions;

/// <summary>
/// Default implementation of <see cref="IConnectionStyle"/> with sensible defaults (gray, 2px, solid).
/// </summary>
public class ConnectionStyle : IConnectionStyle
{
    public IBrush Stroke { get; }
    public double Thickness { get; }
    public IDashStyle? DashPattern { get; }

    public ConnectionStyle(
        IBrush? stroke = null,
        double thickness = 2.0,
        IDashStyle? dashPattern = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thickness);
        Stroke = stroke ?? Brushes.Gray;
        Thickness = thickness;
        DashPattern = dashPattern;
    }
}
