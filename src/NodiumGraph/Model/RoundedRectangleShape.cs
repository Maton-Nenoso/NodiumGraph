using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Rounded rectangle node shape. Computes boundary point using rectangle edges
/// with circular arc corners of the specified radius.
/// </summary>
public class RoundedRectangleShape : INodeShape
{
    /// <summary>
    /// The corner radius. Clamped to at most half the smaller dimension during computation.
    /// </summary>
    public double CornerRadius { get; }

    public RoundedRectangleShape(double cornerRadius = 8.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        CornerRadius = cornerRadius;
    }

    public Point GetBoundaryPoint(double angleDegrees, double width, double height)
    {
        var halfW = width / 2.0;
        var halfH = height / 2.0;
        var r = Math.Min(CornerRadius, Math.Min(halfW, halfH));

        if (r < 1e-12)
        {
            // Degenerate to plain rectangle
            return new RectangleShape().GetBoundaryPoint(angleDegrees, width, height);
        }

        var angleRad = angleDegrees * Math.PI / 180.0;

        // Direction vector: 0=top, clockwise
        var dx = Math.Sin(angleRad);
        var dy = -Math.Cos(angleRad);

        // First, find the rectangle intersection point
        var rectPoint = new RectangleShape().GetBoundaryPoint(angleDegrees, width, height);
        var rx = rectPoint.X;
        var ry = rectPoint.Y;

        // Check if the point falls in a corner region.
        // Corner regions are where |x| > halfW - r AND |y| > halfH - r.
        var innerHalfW = halfW - r;
        var innerHalfH = halfH - r;

        if (Math.Abs(rx) > innerHalfW + 1e-12 && Math.Abs(ry) > innerHalfH + 1e-12)
        {
            // The ray passes through a corner. Intersect with the corner arc.
            // The corner center is at (sign(rx)*innerHalfW, sign(ry)*innerHalfH).
            var cx = Math.Sign(rx) * innerHalfW;
            var cy = Math.Sign(ry) * innerHalfH;

            // Solve for ray-circle intersection: |P + t*D - C|^2 = r^2
            // where P = origin (0,0), D = (dx, dy), C = (cx, cy)
            // t^2 * (dx^2 + dy^2) - 2*t*(dx*cx + dy*cy) + (cx^2 + cy^2 - r^2) = 0
            var a = dx * dx + dy * dy; // should be 1 for unit direction
            var b = -(dx * cx + dy * cy);
            var c = cx * cx + cy * cy - r * r;

            var discriminant = b * b - a * c;
            if (discriminant < 0)
            {
                // Fallback: shouldn't happen for valid geometries
                return rectPoint;
            }

            var sqrtD = Math.Sqrt(discriminant);
            // We want the positive t closest to origin (smallest positive)
            var t1 = (-b - sqrtD) / a;
            var t2 = (-b + sqrtD) / a;

            var t = t1 > 1e-12 ? t1 : t2;
            if (t < 1e-12)
                return rectPoint;

            return new Point(t * dx, t * dy);
        }

        // Not in a corner region, the rectangle point is correct
        return rectPoint;
    }
}
