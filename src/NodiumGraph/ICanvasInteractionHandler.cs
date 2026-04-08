using Avalonia;

namespace NodiumGraph;

public interface ICanvasInteractionHandler
{
    void OnCanvasDoubleClicked(Point worldPosition);
    void OnCanvasDropped(Point worldPosition, object data);
}
