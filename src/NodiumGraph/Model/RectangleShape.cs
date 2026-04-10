using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Rectangular node shape. Computes boundary point by finding the ray-rectangle intersection.
/// </summary>
public class RectangleShape : INodeShape
{
    public Point GetBoundaryPoint(double angleDegrees, double width, double height)
    {
        // Convert angle to radians. 0 = top (negative Y), clockwise.
        var angleRad = angleDegrees * Math.PI / 180.0;

        // Direction vector: sin(angle) for X (right is positive), -cos(angle) for Y (up is negative)
        var dx = Math.Sin(angleRad);
        var dy = -Math.Cos(angleRad);

        var halfW = width / 2.0;
        var halfH = height / 2.0;

        // Find the minimum positive scale factor t such that (t*dx, t*dy) hits a rectangle edge.
        // The rectangle extends from (-halfW, -halfH) to (halfW, halfH).
        var t = double.MaxValue;

        if (Math.Abs(dx) > 1e-12)
        {
            var tx = (dx > 0 ? halfW : -halfW) / dx;
            if (tx > 0) t = Math.Min(t, tx);
        }

        if (Math.Abs(dy) > 1e-12)
        {
            var ty = (dy > 0 ? halfH : -halfH) / dy;
            if (ty > 0) t = Math.Min(t, ty);
        }

        if (t == double.MaxValue)
            t = 0;

        return new Point(t * dx, t * dy);
    }
}
