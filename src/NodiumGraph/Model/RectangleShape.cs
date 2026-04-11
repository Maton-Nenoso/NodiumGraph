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
        var distRight = halfW - cx;
        var distLeft = cx + halfW;
        var distBottom = halfH - cy;
        var distTop = cy + halfH;

        var minDist = distRight;
        var result = new Point(halfW, cy);

        if (distLeft < minDist) { minDist = distLeft; result = new Point(-halfW, cy); }
        if (distBottom < minDist) { minDist = distBottom; result = new Point(cx, halfH); }
        if (distTop < minDist) { result = new Point(cx, -halfH); }

        return result;
    }
}
