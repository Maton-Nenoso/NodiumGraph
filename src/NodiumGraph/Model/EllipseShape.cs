using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Elliptical node shape. Returns the nearest point on the ellipse boundary
/// to the given center-relative position using iterative fixed-point refinement.
/// Also implements anchor-based positioning and outward-normal computation.
/// </summary>
/// <remarks>
/// The stationary condition for the nearest point on (a·cos t, b·sin t) to (px, py) is:
///   a·px·sin t = b·py·cos t + (a²−b²)·sin t·cos t
/// Rearranging gives the fixed-point iteration:
///   t_{n+1} = atan2(b·py + (a²−b²)·sin(t_n), a·px)
/// Starting from the angular projection and running a few iterations converges quickly.
///
/// Anchor angle convention (Avalonia screen coords, +x right, +y down):
///   Top:    θ ∈ [−3π/4, −π/4)   (f=0 at θ=−3π/4, f=1 at θ=−π/4)
///   Right:  θ ∈ [−π/4,  +π/4)
///   Bottom: θ ∈ [+π/4,  +3π/4)
///   Left:   θ ∈ [+3π/4, +5π/4)  (wraps through ±π)
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

        // At the center every boundary point is equidistant in the angular sense;
        // return the top of the ellipse as a stable, deterministic default.
        if (Math.Abs(px) < 1e-12 && Math.Abs(py) < 1e-12)
            return new Point(0, -b);

        // Seed with the angular projection (exact for circles, close for ellipses)
        var t = Math.Atan2(py, px);

        // Fixed-point iteration toward the true nearest-point parameter
        var a2MinusB2 = a * a - b * b;
        for (var i = 0; i < Iterations; i++)
            t = Math.Atan2(b * py + a2MinusB2 * Math.Sin(t), a * px);

        return new Point(a * Math.Cos(t), b * Math.Sin(t));
    }

    public Point GetEdgePoint(PortAnchor anchor, double width, double height)
    {
        var a = width  / 2.0;
        var b = height / 2.0;
        var theta = ThetaFor(anchor);
        return new Point(a + a * Math.Cos(theta), b + b * Math.Sin(theta));
    }

    public Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return anchor.Edge switch
            {
                PortEdge.Left   => new Vector(-1, 0),
                PortEdge.Top    => new Vector( 0, -1),
                PortEdge.Right  => new Vector( 1, 0),
                PortEdge.Bottom => new Vector( 0, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
            };
        }
        var a = width  / 2.0;
        var b = height / 2.0;
        var theta = ThetaFor(anchor);
        var nx = b * Math.Cos(theta);
        var ny = a * Math.Sin(theta);
        var mag = Math.Sqrt(nx * nx + ny * ny);
        return new Vector(nx / mag, ny / mag);
    }

    public PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("InferAnchor requires positive dimensions.");

        var a = width  / 2.0;
        var b = height / 2.0;
        var x = boundaryLocal.X - a;  // center-relative
        var y = boundaryLocal.Y - b;
        var theta = Math.Atan2(y / b, x / a);

        const double EpsCorner = 1e-9;
        const double NegThreePiOver4 = -3.0 * Math.PI / 4.0;
        const double NegPiOver4      = -1.0 * Math.PI / 4.0;
        const double PiOver4         =  1.0 * Math.PI / 4.0;
        const double ThreePiOver4    =  3.0 * Math.PI / 4.0;

        // Canonical-corner endpoints: each corner-midpoint canonicalizes to the *start* of the next edge.
        if (Math.Abs(theta - NegThreePiOver4) < EpsCorner) return new PortAnchor(PortEdge.Top,    0.0);
        if (Math.Abs(theta - NegPiOver4)      < EpsCorner) return new PortAnchor(PortEdge.Right,  0.0);
        if (Math.Abs(theta - PiOver4)         < EpsCorner) return new PortAnchor(PortEdge.Bottom, 0.0);
        if (Math.Abs(theta - ThreePiOver4)    < EpsCorner) return new PortAnchor(PortEdge.Left,   0.0);

        if (theta >= NegThreePiOver4 && theta < NegPiOver4)
            return new PortAnchor(PortEdge.Top,    (theta - NegThreePiOver4) / (Math.PI / 2.0));
        if (theta >= NegPiOver4 && theta < PiOver4)
            return new PortAnchor(PortEdge.Right,  (theta - NegPiOver4) / (Math.PI / 2.0));
        if (theta >= PiOver4 && theta < ThreePiOver4)
            return new PortAnchor(PortEdge.Bottom, (theta - PiOver4) / (Math.PI / 2.0));

        // Left: theta in [3π/4, π] or in [-π, -3π/4) — atan2 wraps; normalize to a continuous [3π/4, 5π/4] range.
        var thetaLeft = theta >= 0 ? theta : theta + 2.0 * Math.PI;
        return new PortAnchor(PortEdge.Left, (thetaLeft - ThreePiOver4) / (Math.PI / 2.0));
    }

    private static double ThetaFor(PortAnchor anchor) => anchor.Edge switch
    {
        PortEdge.Top    => -3.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
        PortEdge.Right  => -1.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
        PortEdge.Bottom =>  1.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
        PortEdge.Left   =>  3.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
        _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
    };
}
