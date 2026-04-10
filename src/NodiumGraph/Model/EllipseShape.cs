using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Elliptical node shape. Computes boundary point using parametric ellipse:
/// x = a * sin(t), y = -b * cos(t), where t = angleDegrees in radians.
/// </summary>
public class EllipseShape : INodeShape
{
    public Point GetBoundaryPoint(double angleDegrees, double width, double height)
    {
        var angleRad = angleDegrees * Math.PI / 180.0;

        var a = width / 2.0;  // horizontal semi-axis
        var b = height / 2.0; // vertical semi-axis

        // Parametric ellipse: at angle t (0=top, clockwise),
        // x = a * sin(t), y = -b * cos(t)
        var x = a * Math.Sin(angleRad);
        var y = -b * Math.Cos(angleRad);

        return new Point(x, y);
    }
}
