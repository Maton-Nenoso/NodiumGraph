using Avalonia;
using Avalonia.Input;

namespace NodiumGraph.Interactions;

/// <summary>
/// Receives canvas-level interactions (double-click, external drag-drop).
/// </summary>
public interface ICanvasInteractionHandler
{
    void OnCanvasDoubleClicked(Point worldPosition);
    void OnCanvasDropped(Point worldPosition, IDataTransfer data);
}
