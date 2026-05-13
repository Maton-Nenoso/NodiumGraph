using System;
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

    /// <inheritdoc/>
    public Point GetEdgePoint(PortAnchor anchor, double width, double height) => anchor.Edge switch
    {
        PortEdge.Top    => new Point(anchor.Fraction * width, 0),
        PortEdge.Right  => new Point(width, anchor.Fraction * height),
        PortEdge.Bottom => new Point((1.0 - anchor.Fraction) * width, height),
        PortEdge.Left   => new Point(0, (1.0 - anchor.Fraction) * height),
        _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
    };

    /// <inheritdoc/>
    public Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height) => anchor.Edge switch
    {
        PortEdge.Left   => new Vector(-1, 0),
        PortEdge.Top    => new Vector(0, -1),
        PortEdge.Right  => new Vector(1, 0),
        PortEdge.Bottom => new Vector(0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
    };

    /// <inheritdoc/>
    public PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("InferAnchor requires positive dimensions.");

        var x = boundaryLocal.X;
        var y = boundaryLocal.Y;

        // Canonical clockwise corners: each corner belongs to "the start of the next edge".
        // top-left → Top(0), top-right → Right(0), bottom-right → Bottom(0), bottom-left → Left(0).
        if (x == 0 && y == 0)          return new PortAnchor(PortEdge.Top,    0.0);
        if (x == width && y == 0)      return new PortAnchor(PortEdge.Right,  0.0);
        if (x == width && y == height) return new PortAnchor(PortEdge.Bottom, 0.0);
        if (x == 0 && y == height)     return new PortAnchor(PortEdge.Left,   0.0);

        // Exact-edge regions.
        if (y == 0)      return new PortAnchor(PortEdge.Top,    x / width);
        if (x == width)  return new PortAnchor(PortEdge.Right,  y / height);
        if (y == height) return new PortAnchor(PortEdge.Bottom, (width - x) / width);
        if (x == 0)      return new PortAnchor(PortEdge.Left,   (height - y) / height);

        // Off-boundary fallback: snap to nearest edge by perpendicular distance, then canonicalize.
        var distTop    = y;
        var distBottom = height - y;
        var distLeft   = x;
        var distRight  = width - x;
        var min = Math.Min(Math.Min(distTop, distBottom), Math.Min(distLeft, distRight));
        if (min == distTop)    return new PortAnchor(PortEdge.Top,    Math.Clamp(x / width,          0, 1));
        if (min == distRight)  return new PortAnchor(PortEdge.Right,  Math.Clamp(y / height,         0, 1));
        if (min == distBottom) return new PortAnchor(PortEdge.Bottom, Math.Clamp((width - x) / width,  0, 1));
        return new PortAnchor(PortEdge.Left, Math.Clamp((height - y) / height, 0, 1));
    }
}
