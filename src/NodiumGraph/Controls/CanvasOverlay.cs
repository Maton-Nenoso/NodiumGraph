using System.Globalization;
using System.Runtime.CompilerServices;
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

    // Cached pens — dirty-tracked by brush/thickness
    private Pen? _cachedSelectedBorderPen;
    private IBrush? _lastSelectedBrush;
    private double _lastSelectedThickness;

    private Pen? _cachedHoveredBorderPen;
    private IBrush? _lastHoveredBrush;
    private double _lastHoveredThickness;

    private Pen? _cachedPortOutlinePen;
    private IBrush? _lastPortOutlineBrush;
    private double _lastPortOutlineThickness;

    // Cached FormattedText for port labels. Key uses a 0.5-px-bucketed font size
    // so continuous zoom reuses entries; bounded to avoid unbounded growth.
    private const int LabelCacheMaxEntries = 256;
    private readonly Dictionary<(string label, double bucketedFontSize, IBrush brush), FormattedText> _labelCache = new();

    // Identity-keyed pen cache for styled borders/ports. Mutating a brush
    // instance in place will NOT invalidate the cached pen — same constraint
    // Avalonia already imposes on directly-held brushes. Bounded to 32.
    private const int StyledPenCacheMaxEntries = 32;
    private readonly Dictionary<(IBrush brush, double thickness), Pen> _styledPenCache
        = new(new BrushThicknessComparer());

    // Origin-centered port geometries, keyed on (shape, bucketed scaled radius).
    // Reused across ports of the same shape/size; translated into place at draw
    // time via PushTransform. Bucketing is 0.5 px so incremental zoom reuses entries.
    private const int PortGeometryCacheMaxEntries = 64;
    private readonly Dictionary<(PortShape shape, double bucketedRadius), Geometry> _portGeometryCache = new();

    public CanvasOverlay(NodiumGraphCanvas canvas)
    {
        _canvas = canvas;
        IsHitTestVisible = false; // Let events pass through to the canvas
    }

    private static Pen GetOrCreatePen(ref Pen? cached, ref IBrush? lastBrush,
        ref double lastThickness, IBrush brush, double thickness)
    {
        if (cached != null && ReferenceEquals(lastBrush, brush)
            && Math.Abs(lastThickness - thickness) < 0.001)
            return cached;

        lastBrush = brush;
        lastThickness = thickness;
        cached = new Pen(brush, thickness);
        return cached;
    }

    private Pen GetOrCreateStyledPen(IBrush brush, double thickness)
    {
        var key = (brush, thickness);
        if (_styledPenCache.TryGetValue(key, out var pen))
            return pen;

        if (_styledPenCache.Count >= StyledPenCacheMaxEntries)
            _styledPenCache.Clear();

        pen = new Pen(brush, thickness);
        _styledPenCache[key] = pen;
        return pen;
    }

    private Geometry GetOrCreatePortGeometry(PortShape shape, double bucketedRadius)
    {
        var key = (shape, bucketedRadius);
        if (_portGeometryCache.TryGetValue(key, out var cached))
            return cached;

        if (_portGeometryCache.Count >= PortGeometryCacheMaxEntries)
            _portGeometryCache.Clear();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            switch (shape)
            {
                case PortShape.Diamond:
                    ctx.BeginFigure(new Point(0, -bucketedRadius), true);
                    ctx.LineTo(new Point(bucketedRadius, 0));
                    ctx.LineTo(new Point(0, bucketedRadius));
                    ctx.LineTo(new Point(-bucketedRadius, 0));
                    ctx.EndFigure(true);
                    break;

                case PortShape.Triangle:
                    ctx.BeginFigure(new Point(0, -bucketedRadius), true);
                    ctx.LineTo(new Point(bucketedRadius, bucketedRadius));
                    ctx.LineTo(new Point(-bucketedRadius, bucketedRadius));
                    ctx.EndFigure(true);
                    break;
            }
        }

        _portGeometryCache[key] = geo;
        return geo;
    }

    private sealed class BrushThicknessComparer
        : IEqualityComparer<(IBrush brush, double thickness)>
    {
        public bool Equals((IBrush brush, double thickness) x, (IBrush brush, double thickness) y)
            => ReferenceEquals(x.brush, y.brush) && x.thickness == y.thickness;

        public int GetHashCode((IBrush brush, double thickness) obj)
            => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.brush), obj.thickness);
    }

    public override void Render(DrawingContext context)
    {
        var graph = _canvas.Graph;
        if (graph == null) return;

        var transform = new ViewportTransform(_canvas.ViewportZoom, _canvas.ViewportOffset);
        var zoom = _canvas.ViewportZoom;

        // Resolve default brushes and thicknesses from theme resources
        var defaultSelectedBrush = _canvas.ResolveBrush(
            NodiumGraphResources.NodeSelectedBorderBrushKey,
            NodiumGraphCanvas.DefaultSelectedBorderBrush);
        var defaultSelectedThickness = ResolveResource<double>(
            NodiumGraphResources.NodeSelectedBorderThicknessKey, 2);
        var defaultHoveredBrush = _canvas.ResolveBrush(
            NodiumGraphResources.NodeHoveredBorderBrushKey,
            NodiumGraphCanvas.DefaultHoveredBorderBrush);
        var defaultHoveredThickness = ResolveResource<double>(
            NodiumGraphResources.NodeHoveredBorderThicknessKey, 1.5);

        var selectedBorderPen = GetOrCreatePen(ref _cachedSelectedBorderPen, ref _lastSelectedBrush,
            ref _lastSelectedThickness, defaultSelectedBrush, defaultSelectedThickness);
        var hoveredBorderPen = GetOrCreatePen(ref _cachedHoveredBorderPen, ref _lastHoveredBrush,
            ref _lastHoveredThickness, defaultHoveredBrush, defaultHoveredThickness);

        // Node state borders (hovered + selected)
        foreach (var node in graph.Nodes)
        {
            if (!node.IsSelected && node != _canvas.HoveredNode) continue;

            var screenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var scaledSize = new Size(node.Width * zoom, node.Height * zoom);
            var nodeRect = new Rect(screenPos, scaledSize).Inflate(2);

            if (node.IsSelected)
            {
                var pen = node.Style?.SelectionBorderBrush != null || node.Style?.SelectionBorderThickness != null
                    ? GetOrCreateStyledPen(
                        node.Style?.SelectionBorderBrush ?? defaultSelectedBrush,
                        node.Style?.SelectionBorderThickness ?? defaultSelectedThickness)
                    : selectedBorderPen;
                context.DrawRectangle(null, pen, nodeRect, 6, 6);
            }
            else if (node == _canvas.HoveredNode)
            {
                var pen = node.Style?.HoverBorderBrush != null || node.Style?.HoverBorderThickness != null
                    ? GetOrCreateStyledPen(
                        node.Style?.HoverBorderBrush ?? defaultHoveredBrush,
                        node.Style?.HoverBorderThickness ?? defaultHoveredThickness)
                    : hoveredBorderPen;
                context.DrawRectangle(null, pen, nodeRect, 6, 6);
            }
        }

        // Snap ghost outline during drag
        if (_canvas.SnapGhostPosition is { } ghostPos)
        {
            var ghostScreen = transform.WorldToScreen(ghostPos);
            var ghostSize = new Size(_canvas.SnapGhostSize.Width * zoom, _canvas.SnapGhostSize.Height * zoom);
            var ghostRect = new Rect(ghostScreen, ghostSize);
            context.DrawRectangle(SnapGhostBrush, null, ghostRect, 6, 6);
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

            var defaultPortPen = GetOrCreatePen(ref _cachedPortOutlinePen, ref _lastPortOutlineBrush,
                ref _lastPortOutlineThickness, defaultPortOutlineBrush, 1.0);

            foreach (var node in graph.Nodes)
            {
                if (node.PortProvider == null) continue;
                if (node.IsCollapsed) continue;
                foreach (var port in node.PortProvider.Ports)
                {
                    var screenPos = transform.WorldToScreen(port.AbsolutePosition);
                    var style = port.Style;

                    var fill = style?.Fill ?? defaultPortBrush;
                    var shape = style?.Shape ?? PortShape.Circle;
                    var radius = style?.Size ?? defaultPortRadius;
                    var scaledRadius = radius * zoom;

                    var pen = (style?.Stroke != null || style?.StrokeWidth != null)
                        ? GetOrCreateStyledPen(style?.Stroke ?? defaultPortOutlineBrush, style?.StrokeWidth ?? 1.0)
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
                        case PortShape.Triangle:
                        {
                            var bucketedRadius = Math.Round(scaledRadius * 2) / 2;
                            var geo = GetOrCreatePortGeometry(shape, bucketedRadius);
                            using (context.PushTransform(Matrix.CreateTranslation(screenPos.X, screenPos.Y)))
                            {
                                context.DrawGeometry(fill, pen, geo);
                            }
                            break;
                        }
                    }
                }
            }

            // Port labels (rendered when PortTemplate is null and port has a label)
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
                    var cacheKey = (port.Label, bucketedFontSize, portLabelBrush);
                    if (!_labelCache.TryGetValue(cacheKey, out var text))
                    {
                        if (_labelCache.Count >= LabelCacheMaxEntries)
                            _labelCache.Clear();

                        text = new FormattedText(
                            port.Label,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            Typeface.Default,
                            bucketedFontSize,
                            portLabelBrush);
                        _labelCache[cacheKey] = text;
                    }

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
