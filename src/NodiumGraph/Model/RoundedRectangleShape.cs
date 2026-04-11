using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Rounded rectangle node shape. Returns the nearest point on the rounded rectangle
/// boundary to the given center-relative position. Straight edges use rectangle
/// clamping; corner regions project onto the corner arc.
/// </summary>
public class RoundedRectangleShape : INodeShape
{
    private static readonly RectangleShape FallbackRectangle = new();

    /// <summary>
    /// The corner radius. Clamped to at most half the smaller dimension during computation.
    /// </summary>
    public double CornerRadius { get; }

    public RoundedRectangleShape(double cornerRadius = 8.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        CornerRadius = cornerRadius;
    }

    public Point GetNearestBoundaryPoint(Point position, double width, double height)
    {
        var halfW = width / 2.0;
        var halfH = height / 2.0;
        var r = Math.Min(CornerRadius, Math.Min(halfW, halfH));
        if (r < 1e-12)
            return FallbackRectangle.GetNearestBoundaryPoint(position, width, height);
        var rectPoint = FallbackRectangle.GetNearestBoundaryPoint(position, width, height);
        var innerHalfW = halfW - r;
        var innerHalfH = halfH - r;
        if (Math.Abs(rectPoint.X) > innerHalfW + 1e-12 && Math.Abs(rectPoint.Y) > innerHalfH + 1e-12)
        {
            var cx = Math.Sign(rectPoint.X) * innerHalfW;
            var cy = Math.Sign(rectPoint.Y) * innerHalfH;
            var dx = position.X - cx;
            var dy = position.Y - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-12)
                return new Point(cx + Math.Sign(rectPoint.X) * r, cy);
            return new Point(cx + dx / dist * r, cy + dy / dist * r);
        }
        return rectPoint;
    }
}
