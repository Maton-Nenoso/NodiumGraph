using Avalonia;
using Avalonia.Media;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

internal static class MinimapRenderer
{
    private const double MinimapWidth = 200.0;
    private const double MinimapHeight = 150.0;
    private const double MinimapPadding = 10.0;

    public static Rect GetMinimapBounds(Rect canvasBounds, MinimapPosition position)
    {
        var margin = 10.0;
        return position switch
        {
            MinimapPosition.BottomRight => new Rect(
                canvasBounds.Width - MinimapWidth - margin,
                canvasBounds.Height - MinimapHeight - margin,
                MinimapWidth, MinimapHeight),
            MinimapPosition.BottomLeft => new Rect(
                margin, canvasBounds.Height - MinimapHeight - margin,
                MinimapWidth, MinimapHeight),
            MinimapPosition.TopRight => new Rect(
                canvasBounds.Width - MinimapWidth - margin,
                margin, MinimapWidth, MinimapHeight),
            MinimapPosition.TopLeft => new Rect(
                margin, margin, MinimapWidth, MinimapHeight),
            _ => new Rect(canvasBounds.Width - MinimapWidth - margin,
                canvasBounds.Height - MinimapHeight - margin,
                MinimapWidth, MinimapHeight)
        };
    }

    public static void Render(DrawingContext context, Rect canvasBounds,
        Graph graph, ViewportTransform viewportTransform, MinimapPosition position,
        IBrush backgroundBrush, IBrush nodeBrush, IBrush selectedNodeBrush, IBrush viewportBrush)
    {
        if (graph.Nodes.Count == 0) return;

        var minimapBounds = GetMinimapBounds(canvasBounds, position);

        var minX = graph.Nodes.Min(n => n.X);
        var minY = graph.Nodes.Min(n => n.Y);
        var maxX = graph.Nodes.Max(n => n.X + n.Width);
        var maxY = graph.Nodes.Max(n => n.Y + n.Height);

        var worldWidth = maxX - minX;
        var worldHeight = maxY - minY;
        if (worldWidth <= 0 || worldHeight <= 0) return;

        // Add padding around world bounds
        minX -= worldWidth * 0.1;
        minY -= worldHeight * 0.1;
        worldWidth *= 1.2;
        worldHeight *= 1.2;

        var scaleX = (minimapBounds.Width - 2 * MinimapPadding) / worldWidth;
        var scaleY = (minimapBounds.Height - 2 * MinimapPadding) / worldHeight;
        var scale = Math.Min(scaleX, scaleY);

        var offsetX = minimapBounds.X + MinimapPadding +
            ((minimapBounds.Width - 2 * MinimapPadding) - worldWidth * scale) / 2;
        var offsetY = minimapBounds.Y + MinimapPadding +
            ((minimapBounds.Height - 2 * MinimapPadding) - worldHeight * scale) / 2;

        // Draw minimap background
        context.DrawRectangle(
            backgroundBrush,
            new Pen(Brushes.Gray, 1),
            minimapBounds, 4, 4);

        // Clip all subsequent drawing to minimap bounds
        using var _ = context.PushClip(new RoundedRect(minimapBounds, 4));

        // Draw nodes as small rectangles
        foreach (var node in graph.Nodes)
        {
            var nodeRect = new Rect(
                offsetX + (node.X - minX) * scale,
                offsetY + (node.Y - minY) * scale,
                Math.Max(node.Width * scale, 4),
                Math.Max(node.Height * scale, 4));

            context.DrawRectangle(
                node.IsSelected ? selectedNodeBrush : nodeBrush,
                null, nodeRect, 1, 1);
        }

        // Draw viewport rectangle
        var viewTopLeft = viewportTransform.ScreenToWorld(canvasBounds.TopLeft);
        var viewBottomRight = viewportTransform.ScreenToWorld(canvasBounds.BottomRight);

        var viewRect = new Rect(
            offsetX + (viewTopLeft.X - minX) * scale,
            offsetY + (viewTopLeft.Y - minY) * scale,
            (viewBottomRight.X - viewTopLeft.X) * scale,
            (viewBottomRight.Y - viewTopLeft.Y) * scale);

        context.DrawRectangle(
            null,
            new Pen(viewportBrush, 1.5),
            viewRect, 2, 2);
    }

    /// <summary>
    /// Converts a click position on the minimap to a world position.
    /// Returns null if the click is outside the minimap bounds.
    /// </summary>
    public static Point? MinimapToWorld(Point screenClick, Rect canvasBounds,
        Graph graph, MinimapPosition position)
    {
        var minimapBounds = GetMinimapBounds(canvasBounds, position);
        if (!minimapBounds.Contains(screenClick)) return null;
        if (graph.Nodes.Count == 0) return null;

        var minX = graph.Nodes.Min(n => n.X);
        var minY = graph.Nodes.Min(n => n.Y);
        var maxX = graph.Nodes.Max(n => n.X + n.Width);
        var maxY = graph.Nodes.Max(n => n.Y + n.Height);

        var worldWidth = (maxX - minX) * 1.2;
        var worldHeight = (maxY - minY) * 1.2;
        minX -= (maxX - minX) * 0.1;
        minY -= (maxY - minY) * 0.1;

        if (worldWidth <= 0 || worldHeight <= 0) return null;

        var scaleX = (minimapBounds.Width - 2 * MinimapPadding) / worldWidth;
        var scaleY = (minimapBounds.Height - 2 * MinimapPadding) / worldHeight;
        var scale = Math.Min(scaleX, scaleY);

        var offsetX = minimapBounds.X + MinimapPadding +
            ((minimapBounds.Width - 2 * MinimapPadding) - worldWidth * scale) / 2;
        var offsetY = minimapBounds.Y + MinimapPadding +
            ((minimapBounds.Height - 2 * MinimapPadding) - worldHeight * scale) / 2;

        var worldX = (screenClick.X - offsetX) / scale + minX;
        var worldY = (screenClick.Y - offsetY) / scale + minY;

        return new Point(worldX, worldY);
    }
}
