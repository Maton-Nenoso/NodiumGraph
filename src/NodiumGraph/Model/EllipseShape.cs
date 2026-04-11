using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Elliptical node shape. Returns the nearest point on the ellipse boundary
/// to the given center-relative position using iterative fixed-point refinement.
/// </summary>
/// <remarks>
/// The stationary condition for the nearest point on (a·cos t, b·sin t) to (px, py) is:
///   a·px·sin t = b·py·cos t + (a²−b²)·sin t·cos t
/// Rearranging gives the fixed-point iteration:
///   t_{n+1} = atan2(b·py + (a²−b²)·sin(t_n), a·px)
/// Starting from the angular projection and running a few iterations converges quickly.
/// </remarks>
public class EllipseShape : INodeShape
{
    private const int Iterations = 10;

    public Point GetNearestBoundaryPoint(Point position, double width, double height)
    {
        var a = width / 2.0;
        var b = height / 2.0;
        if (a < 1e-12 || b < 1e-12)
            return new Point(0, 0);

        var px = position.X;
        var py = position.Y;

        // Seed with the angular projection (exact for circles, close for ellipses)
        var t = Math.Atan2(py, px);

        // Fixed-point iteration toward the true nearest-point parameter
        var a2MinusB2 = a * a - b * b;
        for (var i = 0; i < Iterations; i++)
            t = Math.Atan2(b * py + a2MinusB2 * Math.Sin(t), a * px);

        return new Point(a * Math.Cos(t), b * Math.Sin(t));
    }
}
