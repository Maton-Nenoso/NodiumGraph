using Avalonia.Media;

namespace NodiumGraph.Interactions;

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
        Stroke = stroke ?? Brushes.Gray;
        Thickness = thickness;
        DashPattern = dashPattern;
    }
}
