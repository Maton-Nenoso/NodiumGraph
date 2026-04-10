using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NodiumGraph.Model;

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

        // Resolve brushes/pens from theme resources
        var selectedBorderPen = _canvas.ResolvePen(
            NodiumGraphResources.NodeSelectedBorderBrushKey,
            NodiumGraphCanvas.DefaultSelectedBorderBrush, 2);
        var hoveredBorderPen = _canvas.ResolvePen(
            NodiumGraphResources.NodeHoveredBorderBrushKey,
            NodiumGraphCanvas.DefaultHoveredBorderBrush, 1.5);

        // Node state borders (hovered + selected)
        foreach (var node in graph.Nodes)
        {
            if (!node.IsSelected && node != _canvas.HoveredNode) continue;

            var screenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var scaledSize = new Size(node.Width * zoom, node.Height * zoom);
            var nodeRect = new Rect(screenPos, scaledSize).Inflate(2);

            if (node.IsSelected)
            {
                context.DrawRectangle(null, selectedBorderPen, nodeRect, 6, 6);
            }
            else if (node == _canvas.HoveredNode)
            {
                context.DrawRectangle(null, hoveredBorderPen, nodeRect, 6, 6);
            }
        }

        // Port visuals (only default when no custom template)
        if (_canvas.PortTemplate == null)
        {
            const double defaultPortRadius = 4.0;
            var defaultPortBrush = _canvas.ResolveBrush(
                NodiumGraphResources.PortBrushKey,
                NodiumGraphCanvas.DefaultPortBrush);
            var defaultPortOutlineBrush = _canvas.ResolveBrush(
                NodiumGraphResources.PortOutlineBrushKey,
                NodiumGraphCanvas.DefaultPortOutlineBrush);

            var defaultPortPen = new Pen(defaultPortOutlineBrush, 1.0);

            foreach (var node in graph.Nodes)
            {
                if (node.PortProvider == null) continue;
                foreach (var port in node.PortProvider.Ports)
                {
                    var screenPos = transform.WorldToScreen(port.AbsolutePosition);
                    var style = port.Style;

                    var fill = style?.Fill ?? defaultPortBrush;
                    var shape = style?.Shape ?? PortShape.Circle;
                    var radius = style?.Size ?? defaultPortRadius;
                    var scaledRadius = radius * zoom;

                    var pen = (style?.Stroke != null || style?.StrokeWidth != null)
                        ? new Pen(style?.Stroke ?? defaultPortOutlineBrush, style?.StrokeWidth ?? 1.0)
                        : defaultPortPen;

                    switch (shape)
                    {
                        case PortShape.Circle:
                            context.DrawEllipse(fill, pen,
                                screenPos, scaledRadius, scaledRadius);
                            break;

                        case PortShape.Square:
                            context.DrawRectangle(fill, pen,
                                new Rect(
                                    screenPos.X - scaledRadius,
                                    screenPos.Y - scaledRadius,
                                    scaledRadius * 2,
                                    scaledRadius * 2));
                            break;

                        case PortShape.Diamond:
                        {
                            var geo = new StreamGeometry();
                            using (var ctx = geo.Open())
                            {
                                ctx.BeginFigure(new Point(screenPos.X, screenPos.Y - scaledRadius), true);
                                ctx.LineTo(new Point(screenPos.X + scaledRadius, screenPos.Y));
                                ctx.LineTo(new Point(screenPos.X, screenPos.Y + scaledRadius));
                                ctx.LineTo(new Point(screenPos.X - scaledRadius, screenPos.Y));
                                ctx.EndFigure(true);
                            }
                            context.DrawGeometry(fill, pen, geo);
                            break;
                        }

                        case PortShape.Triangle:
                        {
                            var geo = new StreamGeometry();
                            using (var ctx = geo.Open())
                            {
                                ctx.BeginFigure(new Point(screenPos.X, screenPos.Y - scaledRadius), true);
                                ctx.LineTo(new Point(screenPos.X + scaledRadius, screenPos.Y + scaledRadius));
                                ctx.LineTo(new Point(screenPos.X - scaledRadius, screenPos.Y + scaledRadius));
                                ctx.EndFigure(true);
                            }
                            context.DrawGeometry(fill, pen, geo);
                            break;
                        }
                    }
                }
            }
        }

        // Port highlights during connection draw
        if (_canvas.IsDrawingConnection)
        {
            const double highlightRadius = 7.0;
            var scaledHighlight = highlightRadius * zoom;

            var previewValidPen = _canvas.ResolvePen(
                NodiumGraphResources.ConnectionPreviewValidBrushKey,
                NodiumGraphCanvas.DefaultPreviewValidBrush, 2.0,
                new DashStyle(new double[] { 4, 4 }, 0));
            var cuttingPen = _canvas.ResolvePen(
                NodiumGraphResources.CuttingLineBrushKey,
                NodiumGraphCanvas.DefaultCuttingBrush, 2.0,
                new DashStyle(new double[] { 4, 4 }, 0));

            // Highlight source port
            if (_canvas.ConnectionSourcePort != null)
            {
                var srcScreen = transform.WorldToScreen(_canvas.ConnectionSourcePort.AbsolutePosition);
                context.DrawEllipse(null, previewValidPen,
                    srcScreen, scaledHighlight, scaledHighlight);
            }

            // Highlight target port (green = valid, red = invalid)
            if (_canvas.ConnectionTargetPort != null)
            {
                var tgtScreen = transform.WorldToScreen(_canvas.ConnectionTargetPort.AbsolutePosition);
                var pen = _canvas.ConnectionPreviewValid
                    ? previewValidPen
                    : cuttingPen; // red for invalid
                context.DrawEllipse(null, pen, tgtScreen, scaledHighlight, scaledHighlight);
            }
        }

        // Connection draw preview
        if (_canvas.IsDrawingConnection && _canvas.ConnectionSourcePort != null)
        {
            var startScreen = transform.WorldToScreen(_canvas.ConnectionSourcePort.AbsolutePosition);
            var previewPen = _canvas.ConnectionPreviewValid
                ? _canvas.ResolvePen(
                    NodiumGraphResources.ConnectionPreviewValidBrushKey,
                    NodiumGraphCanvas.DefaultPreviewValidBrush, 2.0,
                    new DashStyle(new double[] { 4, 4 }, 0))
                : _canvas.ResolvePen(
                    NodiumGraphResources.ConnectionPreviewInvalidBrushKey,
                    NodiumGraphCanvas.DefaultPreviewInvalidBrush, 2.0,
                    new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawLine(previewPen, startScreen, _canvas.ConnectionPreviewEnd);
        }

        // Cutting line
        if (_canvas.IsCuttingConnections)
        {
            var cuttingPen = _canvas.ResolvePen(
                NodiumGraphResources.CuttingLineBrushKey,
                NodiumGraphCanvas.DefaultCuttingBrush, 2.0,
                new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawLine(cuttingPen, _canvas.CuttingStart, _canvas.CuttingEnd);
        }

        // Marquee selection rectangle
        if (_canvas.IsMarqueeSelecting)
        {
            var marqueeRect = new Rect(
                Math.Min(_canvas.MarqueeStart.X, _canvas.MarqueeEnd.X),
                Math.Min(_canvas.MarqueeStart.Y, _canvas.MarqueeEnd.Y),
                Math.Abs(_canvas.MarqueeEnd.X - _canvas.MarqueeStart.X),
                Math.Abs(_canvas.MarqueeEnd.Y - _canvas.MarqueeStart.Y));

            var marqueeFill = _canvas.ResolveBrush(
                NodiumGraphResources.MarqueeFillBrushKey,
                NodiumGraphCanvas.DefaultMarqueeFillBrush);
            var marqueePen = _canvas.ResolvePen(
                NodiumGraphResources.MarqueeBorderBrushKey,
                NodiumGraphCanvas.DefaultMarqueeBorderBrush, 1);

            context.DrawRectangle(marqueeFill, marqueePen, marqueeRect);
        }

        // Minimap
        if (_canvas.ShowMinimap)
        {
            var mmBg = _canvas.ResolveBrush(
                NodiumGraphResources.MinimapBackgroundBrushKey,
                NodiumGraphCanvas.DefaultMinimapBackgroundBrush);
            var mmNode = _canvas.ResolveBrush(
                NodiumGraphResources.MinimapNodeBrushKey,
                NodiumGraphCanvas.DefaultMinimapNodeBrush);
            var mmSelected = _canvas.ResolveBrush(
                NodiumGraphResources.MinimapSelectedNodeBrushKey,
                NodiumGraphCanvas.DefaultMinimapSelectedNodeBrush);
            var mmViewport = _canvas.ResolveBrush(
                NodiumGraphResources.MinimapViewportBrushKey,
                NodiumGraphCanvas.DefaultMinimapViewportBrush);
            MinimapRenderer.Render(context, _canvas.Bounds, graph, transform, _canvas.MinimapPosition,
                mmBg, mmNode, mmSelected, mmViewport);
        }
    }
}
