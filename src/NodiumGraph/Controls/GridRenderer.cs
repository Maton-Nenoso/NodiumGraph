using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Controls;

internal static class GridRenderer
{
    public static void Render(DrawingContext context, Rect bounds,
        ViewportTransform transform, double gridSize, GridStyle style,
        IBrush dotBrush, IBrush majorBrush, int majorInterval)
    {
        if (gridSize < 1.0 || style == GridStyle.None) return;

        var opacity = ComputeFadeOpacity(transform.Zoom);
        if (opacity <= 0.0) return;

        var effectiveSize = ComputeEffectiveGridSize(gridSize, transform.Zoom);

        IDisposable? opacityState = opacity < 1.0 ? context.PushOpacity(opacity) : null;
        try
        {
            if (style == GridStyle.Lines)
                RenderLines(context, bounds, transform, effectiveSize, dotBrush, majorBrush, majorInterval, gridSize);
            else
                RenderDots(context, bounds, transform, effectiveSize, dotBrush);
        }
        finally
        {
            opacityState?.Dispose();
        }
    }

    internal static double ComputeFadeOpacity(double zoom)
    {
        if (zoom >= 0.3) return 1.0;
        if (zoom <= 0.1) return 0.0;
        return (zoom - 0.1) / 0.2;
    }

    internal static double ComputeEffectiveGridSize(double gridSize, double zoom)
    {
        var effectiveSize = gridSize;

        // Consolidate: double spacing when screen pixels between lines < 15
        while (effectiveSize * zoom < 15.0)
            effectiveSize *= 2;

        // Subdivide: halve spacing when screen pixels between lines > 60
        // but never go below the base gridSize
        while (effectiveSize * zoom > 60.0 && effectiveSize / 2 >= gridSize)
            effectiveSize /= 2;

        return effectiveSize;
    }

    private static void RenderDots(DrawingContext context, Rect bounds,
        ViewportTransform transform, double gridSize, IBrush dotBrush)
    {
        var (startX, startY, endX, endY) = GetWorldRange(bounds, transform, gridSize);
        var dotRadius = Math.Max(1.0, 1.5 * transform.Zoom);

        for (var x = startX; x <= endX; x += gridSize)
        {
            for (var y = startY; y <= endY; y += gridSize)
            {
                var pt = transform.WorldToScreen(new Point(x, y));
                context.DrawEllipse(dotBrush, null, pt, dotRadius, dotRadius);
            }
        }
    }

    private static void RenderLines(DrawingContext context, Rect bounds,
        ViewportTransform transform, double gridSize, IBrush minorBrush, IBrush majorBrush,
        int majorInterval, double baseGridSize)
    {
        var (startX, startY, endX, endY) = GetWorldRange(bounds, transform, gridSize);
        var majorSpacing = baseGridSize * majorInterval;

        var minorPen = new Pen(minorBrush, 1);
        var majorPen = new Pen(majorBrush, 1);

        // Vertical lines
        for (var x = startX; x <= endX; x += gridSize)
        {
            var isMajor = IsMajor(x, majorSpacing);
            var top = transform.WorldToScreen(new Point(x, startY));
            var bottom = transform.WorldToScreen(new Point(x, endY));
            context.DrawLine(isMajor ? majorPen : minorPen, top, bottom);
        }

        // Horizontal lines
        for (var y = startY; y <= endY; y += gridSize)
        {
            var isMajor = IsMajor(y, majorSpacing);
            var left = transform.WorldToScreen(new Point(startX, y));
            var right = transform.WorldToScreen(new Point(endX, y));
            context.DrawLine(isMajor ? majorPen : minorPen, left, right);
        }
    }

    public static void RenderOriginAxes(DrawingContext context, Rect bounds,
        ViewportTransform transform, IBrush xAxisBrush, IBrush yAxisBrush)
    {
        var origin = transform.WorldToScreen(new Point(0, 0));
        var xPen = new Pen(xAxisBrush, 1.5);
        var yPen = new Pen(yAxisBrush, 1.5);

        // X axis (horizontal line through Y=0)
        if (origin.Y >= bounds.Top && origin.Y <= bounds.Bottom)
            context.DrawLine(xPen, new Point(bounds.Left, origin.Y), new Point(bounds.Right, origin.Y));

        // Y axis (vertical line through X=0)
        if (origin.X >= bounds.Left && origin.X <= bounds.Right)
            context.DrawLine(yPen, new Point(origin.X, bounds.Top), new Point(origin.X, bounds.Bottom));
    }

    internal static IReadOnlyList<Point> ComputeGridPoints(
        Rect visibleScreenArea, ViewportTransform transform, double gridSize)
    {
        if (gridSize < 1.0) return [];

        var (startX, startY, endX, endY) = GetWorldRange(visibleScreenArea, transform, gridSize);
        var points = new List<Point>();
        for (var x = startX; x <= endX; x += gridSize)
        {
            for (var y = startY; y <= endY; y += gridSize)
                points.Add(transform.WorldToScreen(new Point(x, y)));
        }

        return points;
    }

    private static (double startX, double startY, double endX, double endY) GetWorldRange(
        Rect screenBounds, ViewportTransform transform, double gridSize)
    {
        var topLeft = transform.ScreenToWorld(screenBounds.TopLeft);
        var bottomRight = transform.ScreenToWorld(screenBounds.BottomRight);

        var startX = Math.Floor(topLeft.X / gridSize) * gridSize;
        var startY = Math.Floor(topLeft.Y / gridSize) * gridSize;

        return (startX, startY, bottomRight.X, bottomRight.Y);
    }

    private static bool IsMajor(double value, double majorSpacing)
    {
        return Math.Abs(value % majorSpacing) < 0.5;
    }
}
