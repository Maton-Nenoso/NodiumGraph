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
        if (!isSelected && !isHovered) return;

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

        // 2px visual inflate + 6px corner radius, divided by bucketedZoom so the
        // values stay visually constant under the container's ScaleTransform(zoom).
        var inflate = 2.0 / bucketedZoom;
        var cornerRadius = 6.0 / bucketedZoom;
        var rect = new Rect(padX, padY, _node.Width, _node.Height).Inflate(inflate);

        var defaultSelectedBrush = _canvas.ResolveBrush(
            NodiumGraphResources.NodeSelectedBorderBrushKey,
            NodiumGraphCanvas.DefaultSelectedBorderBrush);
        var defaultSelectedThickness = _canvas.ResolveResource<double>(
            NodiumGraphResources.NodeSelectedBorderThicknessKey, 2);

        var defaultHoveredBrush = _canvas.ResolveBrush(
            NodiumGraphResources.NodeHoveredBorderBrushKey,
            NodiumGraphCanvas.DefaultHoveredBorderBrush);
        var defaultHoveredThickness = _canvas.ResolveResource<double>(
            NodiumGraphResources.NodeHoveredBorderThicknessKey, 1.5);

        if (isSelected)
        {
            var brush = _node.Style?.SelectionBorderBrush ?? defaultSelectedBrush;
            var thickness = (_node.Style?.SelectionBorderThickness ?? defaultSelectedThickness) / bucketedZoom;

            var pen = _node.Style?.SelectionBorderBrush != null || _node.Style?.SelectionBorderThickness != null
                ? _canvas.GetOrCreateStyledPen(brush, thickness)
                : _canvas.GetOrCreateSelectedBorderPen(brush, thickness);

            context.DrawRectangle(null, pen, rect, cornerRadius, cornerRadius);
        }
        else
        {
            var brush = _node.Style?.HoverBorderBrush ?? defaultHoveredBrush;
            var thickness = (_node.Style?.HoverBorderThickness ?? defaultHoveredThickness) / bucketedZoom;

            var pen = _node.Style?.HoverBorderBrush != null || _node.Style?.HoverBorderThickness != null
                ? _canvas.GetOrCreateStyledPen(brush, thickness)
                : _canvas.GetOrCreateHoveredBorderPen(brush, thickness);

            context.DrawRectangle(null, pen, rect, cornerRadius, cornerRadius);
        }
    }
}
