using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NodiumGraph.Controls;

/// <summary>
/// Lightweight overlay that renders chrome above node containers:
/// snap ghost, connection draw preview, cutting line, marquee, and minimap.
/// Per-node decorations (borders, port shapes, port labels) live in
/// <see cref="NodeAdornmentLayer"/> so they respect z-order with their container.
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

        // Note: port shapes and labels render per-node in NodeAdornmentLayer so
        // they draw with their container and respect z-order overlap.

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
