using Avalonia.Media;

namespace NodiumGraph;

public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}
