using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Controls;

internal static class GridRenderer
{
    public static IReadOnlyList<Point> ComputeGridPoints(
        Rect visibleScreenArea, ViewportTransform transform, double gridSize)
    {
        if (gridSize < 1.0) return [];

        var topLeft = transform.ScreenToWorld(visibleScreenArea.TopLeft);
        var bottomRight = transform.ScreenToWorld(visibleScreenArea.BottomRight);

        var startX = Math.Floor(topLeft.X / gridSize) * gridSize;
        var startY = Math.Floor(topLeft.Y / gridSize) * gridSize;

        var points = new List<Point>();
        for (var x = startX; x <= bottomRight.X; x += gridSize)
        {
            for (var y = startY; y <= bottomRight.Y; y += gridSize)
            {
                points.Add(transform.WorldToScreen(new Point(x, y)));
            }
        }

        return points;
    }

    public static void Render(DrawingContext context, Rect bounds,
        ViewportTransform transform, double gridSize, IBrush dotBrush)
    {
        var points = ComputeGridPoints(bounds, transform, gridSize);
        var dotRadius = Math.Max(1.0, 1.5 * transform.Zoom);

        foreach (var pt in points)
            context.DrawEllipse(dotBrush, null, pt, dotRadius, dotRadius);
    }
}
