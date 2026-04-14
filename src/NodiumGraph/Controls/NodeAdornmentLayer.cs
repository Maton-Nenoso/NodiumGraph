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
        var zoom = _canvas.ViewportZoom;
        if (zoom <= 0) return;

        var isSelected = _node.IsSelected;
        var isHovered = !isSelected && ReferenceEquals(_canvas.HoveredNode, _node);

        // Node body sits inside the container's symmetric shadow padding.
        // Compute padding from the adornment's Bounds (= container DesiredSize)
        // vs. the node's content size. Works for both padded node templates
        // (ShadowPadding > 0) and zero-padded nodes (CommentNode / GroupNode).
        var padX = (Bounds.Width - _node.Width) / 2;
        var padY = (Bounds.Height - _node.Height) / 2;

        // Bucket zoom to 2 decimals before dividing. Pen thickness goes into
        // single-slot and styled pen caches keyed by (brush, thickness); without
        // bucketing, every frame of a continuous zoom gesture would miss the
        // cache and allocate a new Pen. The 1% visual drift at intermediate
        // zoom values is imperceptible.
        var bucketedZoom = Math.Round(zoom, 2);

        // Selection/hover border.
        if (isSelected || isHovered)
        {
            // 2px visual inflate + 6px corner radius, divided by bucketedZoom so the
            // values stay visually constant under the container's ScaleTransform(zoom).
            var inflate = 2.0 / bucketedZoom;
            var cornerRadius = 6.0 / bucketedZoom;
            var rect = new Rect(padX, padY, _node.Width, _node.Height).Inflate(inflate);

            if (isSelected)
            {
                var defaultSelectedBrush = _canvas.ResolveBrush(
                    NodiumGraphResources.NodeSelectedBorderBrushKey,
                    NodiumGraphCanvas.DefaultSelectedBorderBrush);
                var defaultSelectedThickness = _canvas.ResolveResource<double>(
                    NodiumGraphResources.NodeSelectedBorderThicknessKey, 2);

                var brush = _node.Style?.SelectionBorderBrush ?? defaultSelectedBrush;
                var thickness = (_node.Style?.SelectionBorderThickness ?? defaultSelectedThickness) / bucketedZoom;

                var pen = _node.Style?.SelectionBorderBrush != null || _node.Style?.SelectionBorderThickness != null
                    ? _canvas.GetOrCreateStyledPen(brush, thickness)
                    : _canvas.GetOrCreateSelectedBorderPen(brush, thickness);

                context.DrawRectangle(null, pen, rect, cornerRadius, cornerRadius);
            }
            else
            {
                var defaultHoveredBrush = _canvas.ResolveBrush(
                    NodiumGraphResources.NodeHoveredBorderBrushKey,
                    NodiumGraphCanvas.DefaultHoveredBorderBrush);
                var defaultHoveredThickness = _canvas.ResolveResource<double>(
                    NodiumGraphResources.NodeHoveredBorderThicknessKey, 1.5);

                var brush = _node.Style?.HoverBorderBrush ?? defaultHoveredBrush;
                var thickness = (_node.Style?.HoverBorderThickness ?? defaultHoveredThickness) / bucketedZoom;

                var pen = _node.Style?.HoverBorderBrush != null || _node.Style?.HoverBorderThickness != null
                    ? _canvas.GetOrCreateStyledPen(brush, thickness)
                    : _canvas.GetOrCreateHoveredBorderPen(brush, thickness);

                context.DrawRectangle(null, pen, rect, cornerRadius, cornerRadius);
            }
        }

        // Port visuals (default rendering path — skipped when consumer supplies PortTemplate).
        if (_canvas.PortTemplate != null) return;
        if (_node.PortProvider == null) return;
        if (_node.IsCollapsed) return;

        const double defaultPortRadius = 4.0;
        var defaultPortBrush = _canvas.ResolveBrush(
            NodiumGraphResources.PortBrushKey,
            NodiumGraphCanvas.DefaultPortBrush);
        var defaultPortOutlineBrush = _canvas.ResolveBrush(
            NodiumGraphResources.PortOutlineBrushKey,
            NodiumGraphCanvas.DefaultPortOutlineBrush);

        // 1px visual outline divided by bucketedZoom so it stays 1px under the
        // container's ScaleTransform(zoom).
        var defaultPortPen = _canvas.GetOrCreatePortOutlinePen(defaultPortOutlineBrush, 1.0 / bucketedZoom);

        foreach (var port in _node.PortProvider.Ports)
        {
            var style = port.Style;
            var fill = style?.Fill ?? defaultPortBrush;
            var shape = style?.Shape ?? PortShape.Circle;
            var radius = style?.Size ?? defaultPortRadius;

            var pen = (style?.Stroke != null || style?.StrokeWidth != null)
                ? _canvas.GetOrCreateStyledPen(
                    style?.Stroke ?? defaultPortOutlineBrush,
                    (style?.StrokeWidth ?? 1.0) / bucketedZoom)
                : defaultPortPen;

            // Port position is node-local; offset by padding so it sits inside the body.
            var center = new Point(port.Position.X + padX, port.Position.Y + padY);

            switch (shape)
            {
                case PortShape.Circle:
                    context.DrawEllipse(fill, pen, center, radius, radius);
                    break;

                case PortShape.Square:
                    context.DrawRectangle(fill, pen,
                        new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
                    break;

                case PortShape.Diamond:
                case PortShape.Triangle:
                {
                    var bucketedRadius = Math.Round(radius * 2) / 2;
                    var geo = _canvas.GetOrCreatePortGeometry(shape, bucketedRadius);
                    using (context.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
                    {
                        context.DrawGeometry(fill, pen, geo);
                    }
                    break;
                }
            }
        }

        // Port labels.
        var defaultLabelFontSize = _canvas.ResolveResource<double>(
            NodiumGraphResources.PortLabelFontSizeKey, 11.0);
        var defaultLabelBrush = _canvas.ResolveBrush(
            NodiumGraphResources.PortLabelBrushKey,
            NodiumGraphCanvas.DefaultPortLabelBrush);
        var defaultLabelOffset = _canvas.ResolveResource<double>(
            NodiumGraphResources.PortLabelOffsetKey, 8.0);

        foreach (var port in _node.PortProvider.Ports)
        {
            if (string.IsNullOrEmpty(port.Label)) continue;

            var labelFontSize = port.Style?.LabelFontSize ?? defaultLabelFontSize;
            var labelBrush = port.Style?.LabelBrush ?? defaultLabelBrush;
            var labelOffset = port.Style?.LabelOffset ?? defaultLabelOffset;
            var placement = port.Style?.LabelPlacement ?? GetAutoPlacement(port, _node);

            // FormattedText is built at the unscaled font size — the container's
            // ScaleTransform(zoom) magnifies it visually. Bucket the font size to
            // keep the label cache stable against fractional sizes.
            var bucketedFontSize = Math.Round(labelFontSize * 2) / 2;
            var text = _canvas.GetOrCreateLabel(port.Label!, bucketedFontSize, labelBrush);

            var textWidth = text.Width;
            var textHeight = text.Height;

            // Port position is node-local; offset by padding so it sits inside the body.
            var centerX = port.Position.X + padX;
            var centerY = port.Position.Y + padY;

            Point textOrigin;
            switch (placement)
            {
                case PortLabelPlacement.Left:
                    textOrigin = new Point(
                        centerX - labelOffset - textWidth,
                        centerY - textHeight / 2);
                    break;
                case PortLabelPlacement.Right:
                    textOrigin = new Point(
                        centerX + labelOffset,
                        centerY - textHeight / 2);
                    break;
                case PortLabelPlacement.Above:
                    textOrigin = new Point(
                        centerX - textWidth / 2,
                        centerY - labelOffset - textHeight);
                    break;
                case PortLabelPlacement.Below:
                default:
                    textOrigin = new Point(
                        centerX - textWidth / 2,
                        centerY + labelOffset);
                    break;
            }

            context.DrawText(text, textOrigin);
        }
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
