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
    private static readonly SolidColorBrush SnapGhostBrush = new(Color.FromArgb(77, 255, 255, 255));

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

        // Note: node selection and hover borders are rendered per-node by
        // NodeAdornmentLayer so they respect z-order with their container.

        // Snap ghost outline during drag
        if (_canvas.SnapGhostPosition is { } ghostPos)
        {
            var ghostScreen = transform.WorldToScreen(ghostPos);
            var ghostSize = new Size(_canvas.SnapGhostSize.Width * zoom, _canvas.SnapGhostSize.Height * zoom);
            var ghostRect = new Rect(ghostScreen, ghostSize);
            context.DrawRectangle(SnapGhostBrush, null, ghostRect, 6, 6);
        }

        // Note: port shape rendering lives in NodeAdornmentLayer so each node's
        // ports draw with that node and respect z-order overlap.

        // Port labels (rendered when PortTemplate is null and port has a label)
        if (_canvas.PortTemplate == null)
        {
            var defaultLabelBrush = _canvas.ResolveBrush(
                NodiumGraphResources.PortLabelBrushKey,
                NodiumGraphCanvas.DefaultPortLabelBrush);
            var defaultLabelFontSize = ResolveResource<double>(
                NodiumGraphResources.PortLabelFontSizeKey, 11.0);
            var defaultLabelOffset = ResolveResource<double>(
                NodiumGraphResources.PortLabelOffsetKey, 8.0);

            foreach (var node in graph.Nodes)
            {
                if (node.PortProvider == null) continue;
                if (node.IsCollapsed) continue;
                foreach (var port in node.PortProvider.Ports)
                {
                    if (string.IsNullOrEmpty(port.Label)) continue;

                    var screenPos = transform.WorldToScreen(port.AbsolutePosition);
                    var placement = port.Style?.LabelPlacement ?? GetAutoPlacement(port, node);
                    var portLabelFontSize = port.Style?.LabelFontSize ?? defaultLabelFontSize;
                    var portLabelBrush = port.Style?.LabelBrush ?? defaultLabelBrush;
                    var portLabelOffset = port.Style?.LabelOffset ?? defaultLabelOffset;
                    var scaledOffset = portLabelOffset * zoom;

                    var bucketedFontSize = Math.Round(portLabelFontSize * zoom * 2) / 2;
                    var text = _canvas.GetOrCreateLabel(port.Label, bucketedFontSize, portLabelBrush);

                    var textWidth = text.Width;
                    var textHeight = text.Height;

                    Point textOrigin;
                    switch (placement)
                    {
                        case PortLabelPlacement.Left:
                            textOrigin = new Point(
                                screenPos.X - scaledOffset - textWidth,
                                screenPos.Y - textHeight / 2);
                            break;
                        case PortLabelPlacement.Right:
                            textOrigin = new Point(
                                screenPos.X + scaledOffset,
                                screenPos.Y - textHeight / 2);
                            break;
                        case PortLabelPlacement.Above:
                            textOrigin = new Point(
                                screenPos.X - textWidth / 2,
                                screenPos.Y - scaledOffset - textHeight);
                            break;
                        case PortLabelPlacement.Below:
                        default:
                            textOrigin = new Point(
                                screenPos.X - textWidth / 2,
                                screenPos.Y + scaledOffset);
                            break;
                    }

                    context.DrawText(text, textOrigin);
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

    /// <summary>
    /// Resolves a non-brush resource from the Avalonia resource tree, falling back to a default.
    /// </summary>
    private T ResolveResource<T>(string key, T fallback)
    {
        if (_canvas.TryFindResource(key, out var value) && value is T typed)
            return typed;
        return fallback;
    }

    /// <summary>
    /// Determines label placement based on port position relative to the node center.
    /// Ports on the right half get Right placement; ports on the left half get Left placement.
    /// </summary>
    private static PortLabelPlacement GetAutoPlacement(Port port, Node node)
    {
        var nodeCenter = node.Width / 2.0;
        return port.Position.X >= nodeCenter ? PortLabelPlacement.Right : PortLabelPlacement.Left;
    }
}
