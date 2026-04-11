using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Elliptical node shape. Returns the nearest point on the ellipse boundary
/// to the given center-relative position, using the angle from center as the
/// parametric parameter.
/// </summary>
public class EllipseShape : INodeShape
{
    public Point GetNearestBoundaryPoint(Point position, double width, double height)
    {
        var a = width / 2.0;
        var b = height / 2.0;
        if (a < 1e-12 || b < 1e-12)
            return new Point(0, 0);
        var angle = Math.Atan2(position.Y, position.X);
        return new Point(a * Math.Cos(angle), b * Math.Sin(angle));
    }
}
