using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Rounded rectangle node shape. Returns the nearest point on the rounded rectangle
/// boundary to the given center-relative position. Straight edges use rectangle
/// clamping; corner regions project onto the corner arc.
///
/// Anchor-based positioning uses piecewise arc-length parameterization per edge.
/// Each edge owns half of each adjacent corner arc (45°, arc-length π·rEff/4) plus
/// the flat segment between them. Fraction=0 is at the edge's canonical corner-midpoint;
/// Fraction=1 is at the shared corner-midpoint with the next edge (= next edge's Fraction=0).
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

    /// <inheritdoc/>
    public Point GetEdgePoint(PortAnchor anchor, double width, double height)
    {
        if (width <= 0 || height <= 0)
            return new Point(0, 0);

        var (rEff, flat, halfArc, total, t1, t2) = ComputeEdgeGeometry(anchor.Edge, width, height);
        if (total <= 1e-12)
            return new Point(0, 0);

        var f = anchor.Fraction;
        return ComputePointOnEdge(anchor.Edge, f, rEff, flat, halfArc, total, t1, t2, width, height);
    }

    /// <inheritdoc/>
    public Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height)
    {
        var cardinal = CardinalNormal(anchor.Edge);
        if (width <= 0 || height <= 0)
            return cardinal;

        var (rEff, _, _, total, t1, t2) = ComputeEdgeGeometry(anchor.Edge, width, height);
        if (total <= 1e-12)
            return cardinal;

        var f = anchor.Fraction;
        double angle;

        if (f <= t1)
        {
            // Leading half-arc
            if (rEff < 1e-12)
                return cardinal;
            var u = t1 > 1e-12 ? f / t1 : 0.0;
            angle = Lerp(LeadingAngleStart(anchor.Edge), LeadingAngleEnd(anchor.Edge), u);
            return new Vector(Math.Cos(angle), Math.Sin(angle));
        }
        else if (f <= t2)
        {
            // Flat segment
            return cardinal;
        }
        else
        {
            // Trailing half-arc
            if (rEff < 1e-12)
                return cardinal;
            var u = (1.0 - t2) > 1e-12 ? (f - t2) / (1.0 - t2) : 0.0;
            angle = Lerp(TrailingAngleStart(anchor.Edge), TrailingAngleEnd(anchor.Edge), u);
            return new Vector(Math.Cos(angle), Math.Sin(angle));
        }
    }

    /// <inheritdoc/>
    public PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("InferAnchor requires positive dimensions.");

        var rEff = Math.Min(CornerRadius, Math.Min(width / 2.0, height / 2.0));
        var x = boundaryLocal.X;
        var y = boundaryLocal.Y;

        // Corner centers (node-local, top-left origin)
        // TL=(rEff,rEff), TR=(w-rEff,rEff), BR=(w-rEff,h-rEff), BL=(rEff,h-rEff)

        if (rEff > 1e-12)
        {
            // Check each corner: if the point is on the corner arc (distance ~rEff from corner center),
            // determine which half-arc it belongs to and map back to the canonical edge anchor.
            const double eps = 1e-6;

            // TL corner: arc angles from π (left edge entry) through -3π/4 midpoint to -π/2 (top edge entry)
            //   First half  [π, 5π/4] → Left's trailing arc (t2..1 range of Left edge)
            //   Second half [-3π/4, -π/2] → Top's leading arc (0..t1 range of Top edge)
            //   Corner-midpoint angle: -3π/4 (= 5π/4 mod 2π) → canonicalize to Top(0)
            {
                var dx = x - rEff;
                var dy = y - rEff;
                var distSq = dx * dx + dy * dy;
                if (Math.Abs(distSq - rEff * rEff) < eps * (rEff + 1))
                {
                    var angle = Math.Atan2(dy, dx);
                    // Corner midpoint angle = -3π/4
                    const double midAngle = -3.0 * Math.PI / 4.0;
                    if (Math.Abs(AngleDiff(angle, midAngle)) < 1e-9)
                        return new PortAnchor(PortEdge.Top, 0.0);

                    // Normalize angle to [-π, π] atan2 range, then check:
                    // Left trailing: angle in (π, 5π/4] — treat by normalizing atan2 wrap.
                    // atan2 returns [-π, π]; angles near π (left direction from TL) correspond to Left's trailing arc.
                    // Actually in atan2: angles approaching π from below (e.g. 0.99π) AND -π (which is same point)
                    // For Left trailing: going from angle π downward toward -3π/4 (clockwise in screen coords means increasing angle in atan2).
                    // Let me use the continuous angle approach: map atan2 to [0, 2π) for easier range checks.
                    var angleMod = NormalizeAngle(angle); // [0, 2π)

                    // TL arc spans [π, 3π/2] in [0, 2π) terms:
                    //   [π, 5π/4]   = Left trailing (π to 5π/4 ≡ 180° to 225°)
                    //   [5π/4, 3π/2] = Top leading   (5π/4 to 3π/2 ≡ 225° to 270°, i.e. -3π/4 to -π/2)
                    const double tlStart = Math.PI;           // π   = 180°
                    const double tlMid   = 5.0 * Math.PI / 4.0; // 5π/4 = 225°
                    const double tlEnd   = 3.0 * Math.PI / 2.0; // 3π/2 = 270°

                    if (angleMod >= tlStart - 1e-9 && angleMod <= tlEnd + 1e-9)
                    {
                        if (angleMod <= tlMid + 1e-9)
                        {
                            // Left edge trailing arc: [t2..1] maps to angles [π, 5π/4]
                            var (_, flatLeft, halfArcLeft, totalLeft, t1Left, t2Left) = ComputeEdgeGeometryDirect(PortEdge.Left, width, height, rEff);
                            if (totalLeft > 1e-12)
                            {
                                var u = (angleMod - tlStart) / (Math.PI / 4.0); // 0→1 over [π, 5π/4]
                                var fraction = t2Left + u * (1.0 - t2Left);
                                return new PortAnchor(PortEdge.Left, Math.Clamp(fraction, 0.0, 1.0));
                            }
                        }
                        else
                        {
                            // Top edge leading arc: [0..t1] maps to angles [5π/4, 3π/2] = [-3π/4, -π/2]
                            var (_, _, _, _, t1Top, _) = ComputeEdgeGeometryDirect(PortEdge.Top, width, height, rEff);
                            if (t1Top > 1e-12)
                            {
                                var u = (angleMod - tlMid) / (Math.PI / 4.0); // 0→1 over [5π/4, 3π/2]
                                var fraction = u * t1Top;
                                return new PortAnchor(PortEdge.Top, Math.Clamp(fraction, 0.0, 1.0));
                            }
                        }
                    }
                }
            }

            // TR corner: arc spans [-π/2, 0] in standard terms (top-right quadrant in screen coords)
            //   [-π/2, -π/4] → Top trailing arc    → Top edge [t2..1]
            //   [-π/4,  0]   → Right leading arc   → Right edge [0..t1]
            //   Corner-midpoint: -π/4 → Right(0)
            {
                var dx = x - (width - rEff);
                var dy = y - rEff;
                var distSq = dx * dx + dy * dy;
                if (Math.Abs(distSq - rEff * rEff) < eps * (rEff + 1))
                {
                    var angle = Math.Atan2(dy, dx);
                    const double midAngle = -Math.PI / 4.0;
                    if (Math.Abs(AngleDiff(angle, midAngle)) < 1e-9)
                        return new PortAnchor(PortEdge.Right, 0.0);

                    // TR arc spans [-π/2, 0]:
                    //   [-π/2, -π/4] → Top trailing  (t2..1 of Top)
                    //   [-π/4,  0]   → Right leading (0..t1 of Right)
                    const double trStart  = -Math.PI / 2.0;
                    const double trMid    = -Math.PI / 4.0;
                    const double trEnd    = 0.0;

                    if (angle >= trStart - 1e-9 && angle <= trEnd + 1e-9)
                    {
                        if (angle <= trMid + 1e-9)
                        {
                            // Top trailing: angles [-π/2, -π/4] → Top [t2..1]
                            var (_, _, _, _, _, t2Top) = ComputeEdgeGeometryDirect(PortEdge.Top, width, height, rEff);
                            var u = (angle - trStart) / (Math.PI / 4.0);
                            var fraction = t2Top + u * (1.0 - t2Top);
                            return new PortAnchor(PortEdge.Top, Math.Clamp(fraction, 0.0, 1.0));
                        }
                        else
                        {
                            // Right leading: angles [-π/4, 0] → Right [0..t1]
                            var (_, _, _, _, t1Right, _) = ComputeEdgeGeometryDirect(PortEdge.Right, width, height, rEff);
                            var u = (angle - trMid) / (Math.PI / 4.0);
                            var fraction = u * t1Right;
                            return new PortAnchor(PortEdge.Right, Math.Clamp(fraction, 0.0, 1.0));
                        }
                    }
                }
            }

            // BR corner: arc spans [0, π/2] (bottom-right quadrant in screen coords)
            //   [0, π/4]    → Right trailing → Right [t2..1]
            //   [π/4, π/2]  → Bottom leading → Bottom [0..t1]
            //   Corner-midpoint: π/4 → Bottom(0)
            {
                var dx = x - (width - rEff);
                var dy = y - (height - rEff);
                var distSq = dx * dx + dy * dy;
                if (Math.Abs(distSq - rEff * rEff) < eps * (rEff + 1))
                {
                    var angle = Math.Atan2(dy, dx);
                    const double midAngle = Math.PI / 4.0;
                    if (Math.Abs(AngleDiff(angle, midAngle)) < 1e-9)
                        return new PortAnchor(PortEdge.Bottom, 0.0);

                    const double brStart = 0.0;
                    const double brMid   = Math.PI / 4.0;
                    const double brEnd   = Math.PI / 2.0;

                    if (angle >= brStart - 1e-9 && angle <= brEnd + 1e-9)
                    {
                        if (angle <= brMid + 1e-9)
                        {
                            // Right trailing: [0, π/4] → Right [t2..1]
                            var (_, _, _, _, _, t2Right) = ComputeEdgeGeometryDirect(PortEdge.Right, width, height, rEff);
                            var u = (angle - brStart) / (Math.PI / 4.0);
                            var fraction = t2Right + u * (1.0 - t2Right);
                            return new PortAnchor(PortEdge.Right, Math.Clamp(fraction, 0.0, 1.0));
                        }
                        else
                        {
                            // Bottom leading: [π/4, π/2] → Bottom [0..t1]
                            var (_, _, _, _, t1Bottom, _) = ComputeEdgeGeometryDirect(PortEdge.Bottom, width, height, rEff);
                            var u = (angle - brMid) / (Math.PI / 4.0);
                            var fraction = u * t1Bottom;
                            return new PortAnchor(PortEdge.Bottom, Math.Clamp(fraction, 0.0, 1.0));
                        }
                    }
                }
            }

            // BL corner: arc spans [π/2, π] (bottom-left quadrant in screen coords)
            //   [π/2, 3π/4]  → Bottom trailing → Bottom [t2..1]
            //   [3π/4, π]    → Left leading    → Left [0..t1]
            //   Corner-midpoint: 3π/4 → Left(0)
            {
                var dx = x - rEff;
                var dy = y - (height - rEff);
                var distSq = dx * dx + dy * dy;
                if (Math.Abs(distSq - rEff * rEff) < eps * (rEff + 1))
                {
                    var angle = Math.Atan2(dy, dx);
                    const double midAngle = 3.0 * Math.PI / 4.0;
                    if (Math.Abs(AngleDiff(angle, midAngle)) < 1e-9)
                        return new PortAnchor(PortEdge.Left, 0.0);

                    const double blStart = Math.PI / 2.0;
                    const double blMid   = 3.0 * Math.PI / 4.0;
                    const double blEnd   = Math.PI;

                    if (angle >= blStart - 1e-9 && angle <= blEnd + 1e-9)
                    {
                        if (angle <= blMid + 1e-9)
                        {
                            // Bottom trailing: [π/2, 3π/4] → Bottom [t2..1]
                            var (_, _, _, _, _, t2Bottom) = ComputeEdgeGeometryDirect(PortEdge.Bottom, width, height, rEff);
                            var u = (angle - blStart) / (Math.PI / 4.0);
                            var fraction = t2Bottom + u * (1.0 - t2Bottom);
                            return new PortAnchor(PortEdge.Bottom, Math.Clamp(fraction, 0.0, 1.0));
                        }
                        else
                        {
                            // Left leading: [3π/4, π] → Left [0..t1]
                            var (_, _, _, _, t1Left, _) = ComputeEdgeGeometryDirect(PortEdge.Left, width, height, rEff);
                            var u = (angle - blMid) / (Math.PI / 4.0);
                            var fraction = u * t1Left;
                            return new PortAnchor(PortEdge.Left, Math.Clamp(fraction, 0.0, 1.0));
                        }
                    }
                }
            }
        }

        // Point is on a flat segment.
        // Identify which edge and compute fraction in [t1, t2].
        const double flatEps = 1e-6;

        if (Math.Abs(y) < flatEps && x >= rEff - flatEps && x <= width - rEff + flatEps)
        {
            // Top flat: y==0, x in [rEff, w-rEff]
            var (_, flatTop, _, _, t1Top2, t2Top2) = ComputeEdgeGeometryDirect(PortEdge.Top, width, height, rEff);
            if (flatTop > 1e-12)
            {
                var u = (x - rEff) / flatTop;
                return new PortAnchor(PortEdge.Top, t1Top2 + u * (t2Top2 - t1Top2));
            }
            return new PortAnchor(PortEdge.Top, 0.5);
        }

        if (Math.Abs(x - width) < flatEps && y >= rEff - flatEps && y <= height - rEff + flatEps)
        {
            // Right flat: x==w, y in [rEff, h-rEff]
            var (_, flatRight, _, _, t1Right2, t2Right2) = ComputeEdgeGeometryDirect(PortEdge.Right, width, height, rEff);
            if (flatRight > 1e-12)
            {
                var u = (y - rEff) / flatRight;
                return new PortAnchor(PortEdge.Right, t1Right2 + u * (t2Right2 - t1Right2));
            }
            return new PortAnchor(PortEdge.Right, 0.5);
        }

        if (Math.Abs(y - height) < flatEps && x >= rEff - flatEps && x <= width - rEff + flatEps)
        {
            // Bottom flat: y==h, x in [rEff, w-rEff] — note Bottom traverses RIGHT to LEFT (decreasing x)
            var (_, flatBottom, _, _, t1Bottom2, t2Bottom2) = ComputeEdgeGeometryDirect(PortEdge.Bottom, width, height, rEff);
            if (flatBottom > 1e-12)
            {
                var u = (width - rEff - x) / flatBottom;
                return new PortAnchor(PortEdge.Bottom, t1Bottom2 + u * (t2Bottom2 - t1Bottom2));
            }
            return new PortAnchor(PortEdge.Bottom, 0.5);
        }

        if (Math.Abs(x) < flatEps && y >= rEff - flatEps && y <= height - rEff + flatEps)
        {
            // Left flat: x==0, y in [rEff, h-rEff] — Left traverses BOTTOM to TOP (decreasing y)
            var (_, flatLeft, _, _, t1Left2, t2Left2) = ComputeEdgeGeometryDirect(PortEdge.Left, width, height, rEff);
            if (flatLeft > 1e-12)
            {
                var u = (height - rEff - y) / flatLeft;
                return new PortAnchor(PortEdge.Left, t1Left2 + u * (t2Left2 - t1Left2));
            }
            return new PortAnchor(PortEdge.Left, 0.5);
        }

        // Fallback: snap to nearest edge (handles off-boundary inputs gracefully).
        var distTop    = y;
        var distBottom = height - y;
        var distLeft   = x;
        var distRight  = width - x;
        var min = Math.Min(Math.Min(distTop, distBottom), Math.Min(distLeft, distRight));
        if (min == distTop)    return new PortAnchor(PortEdge.Top,    Math.Clamp(x / width, 0, 1));
        if (min == distRight)  return new PortAnchor(PortEdge.Right,  Math.Clamp(y / height, 0, 1));
        if (min == distBottom) return new PortAnchor(PortEdge.Bottom, Math.Clamp((width - x) / width, 0, 1));
        return new PortAnchor(PortEdge.Left, Math.Clamp((height - y) / height, 0, 1));
    }

    // ---- Private geometry helpers ----

    private (double rEff, double flat, double halfArc, double total, double t1, double t2)
        ComputeEdgeGeometry(PortEdge edge, double width, double height)
    {
        var rEff = Math.Min(CornerRadius, Math.Min(width / 2.0, height / 2.0));
        return ComputeEdgeGeometryDirect(edge, width, height, rEff);
    }

    private static (double rEff, double flat, double halfArc, double total, double t1, double t2)
        ComputeEdgeGeometryDirect(PortEdge edge, double width, double height, double rEff)
    {
        var flat = edge is PortEdge.Top or PortEdge.Bottom
            ? Math.Max(0, width  - 2 * rEff)
            : Math.Max(0, height - 2 * rEff);
        var halfArc = Math.PI * rEff / 4.0;
        var total   = flat + 2 * halfArc;

        double t1, t2;
        if (total <= 1e-12)
        {
            t1 = 0.5;
            t2 = 0.5;
        }
        else if (flat <= 1e-12)
        {
            // Capsule: no flat segment — leading fills [0, 0.5], trailing fills [0.5, 1]
            t1 = 0.5;
            t2 = 0.5;
        }
        else
        {
            t1 = halfArc / total;
            t2 = (halfArc + flat) / total;
        }

        return (rEff, flat, halfArc, total, t1, t2);
    }

    private static Point ComputePointOnEdge(
        PortEdge edge, double f,
        double rEff, double flat, double halfArc, double total,
        double t1, double t2,
        double width, double height)
    {
        if (f <= t1)
        {
            // Leading half-arc (capsule: t1=0.5, normal: t1=halfArc/total).
            var u = t1 > 1e-12 ? f / t1 : 0.0;
            var angle = Lerp(LeadingAngleStart(edge), LeadingAngleEnd(edge), u);
            var (cx, cy) = LeadingCornerCenter(edge, width, height, rEff);
            return new Point(cx + rEff * Math.Cos(angle), cy + rEff * Math.Sin(angle));
        }
        else if (f <= t2)
        {
            // Flat region (when capsule: t1=t2=0.5 so this branch is only hit at f==0.5 exactly,
            // and FlatStart==FlatEnd so any u gives the correct midpoint).
            var u = (t2 - t1) > 1e-12 ? (f - t1) / (t2 - t1) : 0.0;
            var (fx0, fy0) = FlatStart(edge, width, height, rEff);
            var (fx1, fy1) = FlatEnd(edge, width, height, rEff);
            return new Point(Lerp(fx0, fx1, u), Lerp(fy0, fy1, u));
        }
        else
        {
            // Trailing half-arc (capsule: 1-t2=0.5, normal: 1-t2=halfArc/total).
            var u = (1.0 - t2) > 1e-12 ? (f - t2) / (1.0 - t2) : 0.0;
            var angle = Lerp(TrailingAngleStart(edge), TrailingAngleEnd(edge), u);
            var (cx, cy) = TrailingCornerCenter(edge, width, height, rEff);
            return new Point(cx + rEff * Math.Cos(angle), cy + rEff * Math.Sin(angle));
        }
    }

    // Leading arc angles (clockwise traversal, second half of previous corner).
    // Each leading arc spans exactly π/4 (= 45°).
    //
    // Top:    TL corner, arc from its midpoint (-3π/4) to the top-edge entry (-π/2).
    // Right:  TR corner, arc from its midpoint (-π/4) to the right-edge entry (0).
    // Bottom: BR corner, arc from its midpoint (π/4) to the bottom-edge entry (π/2).
    // Left:   BL corner, arc from its midpoint (3π/4) to the left-edge entry (π).
    private static double LeadingAngleStart(PortEdge edge) => edge switch
    {
        PortEdge.Top    => -3.0 * Math.PI / 4.0,
        PortEdge.Right  => -1.0 * Math.PI / 4.0,
        PortEdge.Bottom =>  1.0 * Math.PI / 4.0,
        PortEdge.Left   =>  3.0 * Math.PI / 4.0,
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    private static double LeadingAngleEnd(PortEdge edge) => edge switch
    {
        PortEdge.Top    => -1.0 * Math.PI / 2.0,
        PortEdge.Right  =>  0.0,
        PortEdge.Bottom =>  1.0 * Math.PI / 2.0,
        PortEdge.Left   =>  Math.PI,               // left-edge entry on BL corner (leftmost point = angle π from center)
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    // Trailing arc angles (first half of next corner).
    // Each trailing arc spans exactly π/4 (= 45°).
    //
    // Top:    TR corner, arc from right-edge entry (-π/2 from TR) to TR midpoint (-π/4).
    // Right:  BR corner, arc from bottom-edge entry (0 from BR) to BR midpoint (π/4).
    // Bottom: BL corner, arc from left-edge entry (π/2 from BL) to BL midpoint (3π/4).
    // Left:   TL corner, arc from top-edge entry (π from TL) to TL midpoint (5π/4 = -3π/4 + 2π).
    private static double TrailingAngleStart(PortEdge edge) => edge switch
    {
        PortEdge.Top    => -1.0 * Math.PI / 2.0,
        PortEdge.Right  =>  0.0,
        PortEdge.Bottom =>  1.0 * Math.PI / 2.0,
        PortEdge.Left   =>  Math.PI,               // top-edge entry on TL corner (leftmost point = angle π from TL center)
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    private static double TrailingAngleEnd(PortEdge edge) => edge switch
    {
        PortEdge.Top    => -1.0 * Math.PI / 4.0,
        PortEdge.Right  =>  1.0 * Math.PI / 4.0,
        PortEdge.Bottom =>  3.0 * Math.PI / 4.0,
        PortEdge.Left   =>  5.0 * Math.PI / 4.0,  // TL corner midpoint (= -3π/4 mod 2π); continuous from π
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    // Leading corner center (node-local, top-left origin)
    private static (double cx, double cy) LeadingCornerCenter(PortEdge edge, double w, double h, double rEff) => edge switch
    {
        PortEdge.Top    => (rEff, rEff),             // TL
        PortEdge.Right  => (w - rEff, rEff),         // TR
        PortEdge.Bottom => (w - rEff, h - rEff),     // BR
        PortEdge.Left   => (rEff, h - rEff),         // BL
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    // Trailing corner center (node-local, top-left origin)
    private static (double cx, double cy) TrailingCornerCenter(PortEdge edge, double w, double h, double rEff) => edge switch
    {
        PortEdge.Top    => (w - rEff, rEff),         // TR
        PortEdge.Right  => (w - rEff, h - rEff),     // BR
        PortEdge.Bottom => (rEff, h - rEff),         // BL
        PortEdge.Left   => (rEff, rEff),             // TL
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    // Flat segment start/end points (node-local)
    private static (double x, double y) FlatStart(PortEdge edge, double w, double h, double rEff) => edge switch
    {
        PortEdge.Top    => (rEff, 0),
        PortEdge.Right  => (w, rEff),
        PortEdge.Bottom => (w - rEff, h),
        PortEdge.Left   => (0, h - rEff),
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    private static (double x, double y) FlatEnd(PortEdge edge, double w, double h, double rEff) => edge switch
    {
        PortEdge.Top    => (w - rEff, 0),
        PortEdge.Right  => (w, h - rEff),
        PortEdge.Bottom => (rEff, h),
        PortEdge.Left   => (0, rEff),
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    private static Vector CardinalNormal(PortEdge edge) => edge switch
    {
        PortEdge.Top    => new Vector( 0, -1),
        PortEdge.Right  => new Vector( 1,  0),
        PortEdge.Bottom => new Vector( 0,  1),
        PortEdge.Left   => new Vector(-1,  0),
        _ => throw new ArgumentOutOfRangeException(nameof(edge)),
    };

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Returns the angle difference (a - b) normalized to [-π, π].</summary>
    private static double AngleDiff(double a, double b)
    {
        var d = a - b;
        while (d >  Math.PI) d -= 2 * Math.PI;
        while (d < -Math.PI) d += 2 * Math.PI;
        return d;
    }

    /// <summary>Normalizes an angle to [0, 2π).</summary>
    private static double NormalizeAngle(double angle)
    {
        var a = angle % (2 * Math.PI);
        if (a < 0) a += 2 * Math.PI;
        return a;
    }
}
