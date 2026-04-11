using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Rectangular node shape. Returns the nearest point on the rectangle boundary
/// to the given center-relative position.
/// </summary>
public class RectangleShape : INodeShape
{
    public Point GetNearestBoundaryPoint(Point position, double width, double height)
    {
        var halfW = width / 2.0;
        var halfH = height / 2.0;
        var cx = Math.Clamp(position.X, -halfW, halfW);
        var cy = Math.Clamp(position.Y, -halfH, halfH);
        if (cx != position.X || cy != position.Y)
            return new Point(cx, cy);
        // Inside: snap to nearest edge
        var distLeft = cx + halfW;
        var distRight = halfW - cx;
        var distTop = cy + halfH;
        var distBottom = halfH - cy;
        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));
        if (minDist == distRight) return new Point(halfW, cy);
        if (minDist == distLeft) return new Point(-halfW, cy);
        if (minDist == distBottom) return new Point(cx, halfH);
        return new Point(cx, -halfH);
    }
}
