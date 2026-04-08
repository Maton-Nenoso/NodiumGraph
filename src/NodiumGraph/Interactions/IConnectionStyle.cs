using Avalonia.Media;

namespace NodiumGraph.Interactions;

public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}
