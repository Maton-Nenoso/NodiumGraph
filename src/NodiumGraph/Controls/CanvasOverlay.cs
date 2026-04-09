using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NodiumGraph.Controls;

/// <summary>
/// Lightweight overlay that renders on top of node containers.
/// Draws ports, connection preview, cutting line, marquee, and minimap.
/// </summary>
internal class CanvasOverlay : Control
{
    private readonly NodiumGraphCanvas _canvas;

    public CanvasOverlay(NodiumGraphCanvas canvas)
    {
        _canvas = canvas;
        IsHitTestVisible = false; // Let events pass through to the canvas
    }

    public override void Render(DrawingContext context)
    {
        var graph = _canvas.Graph;
        if (graph == null) return;

        var transform = new ViewportTransform(_canvas.ViewportZoom, _canvas.ViewportOffset);
        var zoom = _canvas.ViewportZoom;

        // Node state borders (hovered + selected)
        foreach (var node in graph.Nodes)
        {
            if (!node.IsSelected && node != _canvas.HoveredNode) continue;

            var screenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var scaledSize = new Size(node.Width * zoom, node.Height * zoom);
            var nodeRect = new Rect(screenPos, scaledSize).Inflate(2);

            if (node.IsSelected)
            {
                context.DrawRectangle(null, NodiumGraphCanvas.s_selectedBorderPen, nodeRect, 6, 6);
            }
            else if (node == _canvas.HoveredNode)
            {
                context.DrawRectangle(null, NodiumGraphCanvas.s_hoveredBorderPen, nodeRect, 6, 6);
            }
        }

        // Port visuals (only default when no custom template)
        if (_canvas.PortTemplate == null)
        {
            const double portRadius = 4.0;
            foreach (var node in graph.Nodes)
            {
                if (node.PortProvider == null) continue;
                foreach (var port in node.PortProvider.Ports)
                {
                    var screenPos = transform.WorldToScreen(port.AbsolutePosition);
                    var scaledRadius = portRadius * _canvas.ViewportZoom;
                    context.DrawEllipse(NodiumGraphCanvas.s_portBrush, NodiumGraphCanvas.s_portOutlinePen,
                        screenPos, scaledRadius, scaledRadius);
                }
            }
        }

        // Connection draw preview
        if (_canvas.IsDrawingConnection && _canvas.ConnectionSourcePort != null)
        {
            var startScreen = transform.WorldToScreen(_canvas.ConnectionSourcePort.AbsolutePosition);
            var previewPen = _canvas.ConnectionPreviewValid
                ? NodiumGraphCanvas.s_previewPenValid
                : NodiumGraphCanvas.s_previewPenInvalid;
            context.DrawLine(previewPen, startScreen, _canvas.ConnectionPreviewEnd);
        }

        // Cutting line
        if (_canvas.IsCuttingConnections)
        {
            context.DrawLine(NodiumGraphCanvas.s_cuttingPen, _canvas.CuttingStart, _canvas.CuttingEnd);
        }

        // Marquee selection rectangle
        if (_canvas.IsMarqueeSelecting)
        {
            var marqueeRect = new Rect(
                Math.Min(_canvas.MarqueeStart.X, _canvas.MarqueeEnd.X),
                Math.Min(_canvas.MarqueeStart.Y, _canvas.MarqueeEnd.Y),
                Math.Abs(_canvas.MarqueeEnd.X - _canvas.MarqueeStart.X),
                Math.Abs(_canvas.MarqueeEnd.Y - _canvas.MarqueeStart.Y));

            context.DrawRectangle(NodiumGraphCanvas.s_marqueeFill, NodiumGraphCanvas.s_marqueePen, marqueeRect);
        }

        // Minimap
        if (_canvas.ShowMinimap)
        {
            MinimapRenderer.Render(context, _canvas.Bounds, graph, transform, _canvas.MinimapPosition);
        }
    }
}
