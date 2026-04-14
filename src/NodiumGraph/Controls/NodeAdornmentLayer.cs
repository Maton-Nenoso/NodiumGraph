using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// Internal per-node adornment control. Draws selection/hover border, port shapes,
/// and port labels in node-local coordinates so that each node's decorations render
/// with that node (respecting z-order overlap) instead of in a global overlay pass.
/// Non-hit-testable; pointer input is resolved centrally by <see cref="NodiumGraphCanvas"/>.
/// </summary>
internal sealed class NodeAdornmentLayer : Control
{
    private readonly NodiumGraphCanvas _canvas;
    private readonly Node _node;

    public NodeAdornmentLayer(NodiumGraphCanvas canvas, Node node)
    {
        _canvas = canvas;
        _node = node;
        IsHitTestVisible = false;
    }

    internal Node Node => _node;

    public override void Render(DrawingContext context)
    {
        // Tasks 3–5 fill this in: selection border → port shapes → port labels.
    }
}
