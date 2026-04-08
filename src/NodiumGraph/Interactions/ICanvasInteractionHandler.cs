using Avalonia;

namespace NodiumGraph.Interactions;

public interface ICanvasInteractionHandler
{
    void OnCanvasDoubleClicked(Point worldPosition);
    void OnCanvasDropped(Point worldPosition, object data);
}
