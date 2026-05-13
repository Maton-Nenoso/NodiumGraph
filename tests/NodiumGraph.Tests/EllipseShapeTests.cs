using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class EllipseShapeTests
{
    private const double Tolerance = 0.001;
    private readonly EllipseShape _shape = new();

    // ---- Cardinal-direction queries: same result for any nearest-point algorithm ----

    [Fact]
    public void Above_center_returns_top_of_ellipse()
    {
        // Position directly above → nearest point is the top vertex (0, -b)
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, -100), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Below_center_returns_bottom_of_ellipse()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, 100), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    [Fact]
    public void Left_of_center_returns_left_of_ellipse()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(-200, 0), 100, 80);
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Right_of_center_returns_right_of_ellipse()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(200, 0), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Result_lies_on_ellipse_boundary_for_arbitrary_position()
    {
        // Any non-center point should produce a result on the ellipse
        var a = 50.0;
        var b = 40.0;
        var pt = _shape.GetNearestBoundaryPoint(new Point(30, -25), 100, 80);
        var onEllipse = (pt.X / a) * (pt.X / a) + (pt.Y / b) * (pt.Y / b);
        Assert.Equal(1.0, onEllipse, Tolerance);
    }

    [Fact]
    public void Result_lies_on_ellipse_for_diagonal_position()
    {
        var a = 60.0;
        var b = 40.0;
        var pt = _shape.GetNearestBoundaryPoint(new Point(45, 30), 120, 80);
        var onEllipse = (pt.X / a) * (pt.X / a) + (pt.Y / b) * (pt.Y / b);
        Assert.Equal(1.0, onEllipse, Tolerance);
    }

    [Fact]
    public void Inside_point_still_returns_boundary_point()
    {
        // Point inside ellipse → nearest point on the boundary is still returned
        var a = 50.0;
        var b = 40.0;
        var pt = _shape.GetNearestBoundaryPoint(new Point(10, 5), 100, 80);
        var onEllipse = (pt.X / a) * (pt.X / a) + (pt.Y / b) * (pt.Y / b);
        Assert.Equal(1.0, onEllipse, Tolerance);
    }

    [Fact]
    public void Circle_all_cardinal_directions_at_correct_radius()
    {
        var r = 50.0;
        var up = _shape.GetNearestBoundaryPoint(new Point(0, -100), 100, 100);
        var right = _shape.GetNearestBoundaryPoint(new Point(100, 0), 100, 100);
        var down = _shape.GetNearestBoundaryPoint(new Point(0, 100), 100, 100);
        var left = _shape.GetNearestBoundaryPoint(new Point(-100, 0), 100, 100);

        Assert.Equal(r, Math.Sqrt(up.X * up.X + up.Y * up.Y), Tolerance);
        Assert.Equal(r, Math.Sqrt(right.X * right.X + right.Y * right.Y), Tolerance);
        Assert.Equal(r, Math.Sqrt(down.X * down.X + down.Y * down.Y), Tolerance);
        Assert.Equal(r, Math.Sqrt(left.X * left.X + left.Y * left.Y), Tolerance);
    }

    [Fact]
    public void Degenerate_zero_size_returns_origin()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(100, 50), 0, 0);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    // ---- Non-circular ellipse: verifies the iterative nearest-point solver ----
    // a=50, b=5 is highly elongated. Angular projection would return a point near
    // the far tip, but the true nearest point hugs the long side of the ellipse.

    [Fact]
    public void Highly_elongated_ellipse_returns_nearest_not_angular_projection()
    {
        // Ellipse: width=100 (a=50), height=10 (b=5). Very elongated horizontally.
        // Query point: (0, 20) — directly above the center but off-axis.
        // Angular projection: atan2(20,0) = 90° → (0, 5) — the very top of the ellipse.
        // True nearest point: also (0, 5) in this cardinal case, so that's not the test.
        //
        // Better test: query at (10, 20). Angular projection gives angle ≈ atan2(20,10),
        // which projects to the far-right quadrant tip. The true nearest point should
        // be very close to (10, 5) because the top of the ellipse at x=10 is only
        // b*sqrt(1 - (10/50)^2) ≈ 4.9 units away, whereas angular projection gives
        // a point much farther from (10, 20).
        const double a = 50.0;
        const double b = 5.0;
        var pt = _shape.GetNearestBoundaryPoint(new Point(10, 20), 100, 10);

        // Result must be on the ellipse boundary
        var onEllipse = (pt.X / a) * (pt.X / a) + (pt.Y / b) * (pt.Y / b);
        Assert.Equal(1.0, onEllipse, Tolerance);

        // The true nearest point is near the top of the ellipse at x≈10.
        // Its Y should be ≈ +b (positive, toward the query) and close to 5.
        Assert.True(pt.Y > 0, "Nearest point should be on the same side as the query (positive Y)");
        Assert.True(pt.Y >= 4.0, $"Nearest point Y={pt.Y:F4} should be near +b=5 (top of ellipse), not near 0");

        // Compute distance from query to the result, and compare against
        // what the naive angular-projection would give.
        double dx = pt.X - 10, dy = pt.Y - 20;
        double distToResult = Math.Sqrt(dx * dx + dy * dy);

        // Angular-projection competitor: angle = atan2(20,10)
        double naiveAngle = Math.Atan2(20, 10);
        double naivePx = a * Math.Cos(naiveAngle);
        double naivePy = b * Math.Sin(naiveAngle);
        double ndx = naivePx - 10, ndy = naivePy - 20;
        double distNaive = Math.Sqrt(ndx * ndx + ndy * ndy);

        Assert.True(distToResult <= distNaive + 1e-6,
            $"Iterative result distance {distToResult:F4} should be ≤ naive angular distance {distNaive:F4}");
    }
}

public class EllipseShapeAnchorTests
{
    private static readonly EllipseShape Shape = new();

    [Theory]
    // Per-edge corner-midpoint endpoints, 200×100 ellipse:
    [InlineData(PortEdge.Top,    0.0, 200, 100,  29.289, 14.645)]  // θ=-3π/4 → (100 + 100·cos(-3π/4), 50 + 50·sin(-3π/4))
    [InlineData(PortEdge.Right,  0.0, 200, 100, 170.711, 14.645)]  // θ=-π/4
    [InlineData(PortEdge.Bottom, 0.0, 200, 100, 170.711, 85.355)]  // θ=π/4
    [InlineData(PortEdge.Left,   0.0, 200, 100,  29.289, 85.355)]  // θ=3π/4
    // Edge midpoints:
    [InlineData(PortEdge.Top,    0.5, 200, 100, 100.0,   0.0)]
    [InlineData(PortEdge.Right,  0.5, 200, 100, 200.0,  50.0)]
    [InlineData(PortEdge.Bottom, 0.5, 200, 100, 100.0, 100.0)]
    [InlineData(PortEdge.Left,   0.5, 200, 100,   0.0,  50.0)]
    public void GetEdgePoint_matches_angle_table(PortEdge edge, double f, double w, double h, double expectedX, double expectedY)
    {
        var p = Shape.GetEdgePoint(new PortAnchor(edge, f), w, h);
        Assert.Equal(expectedX, p.X, 3);
        Assert.Equal(expectedY, p.Y, 3);
    }

    [Fact]
    public void Aspect_aware_outward_normal_differs_from_circle()
    {
        var ellipse = Shape.GetEdgeOutwardNormal(new PortAnchor(PortEdge.Right, 0.0), 200, 100);
        var circle  = Shape.GetEdgeOutwardNormal(new PortAnchor(PortEdge.Right, 0.0), 100, 100);
        Assert.Equal(0.447, ellipse.X, 3);
        Assert.Equal(-0.894, ellipse.Y, 3);
        Assert.Equal(0.707, circle.X, 3);
        Assert.Equal(-0.707, circle.Y, 3);
    }

    [Theory]
    [InlineData(PortEdge.Top,    0.25)]
    [InlineData(PortEdge.Right,  0.5)]
    [InlineData(PortEdge.Bottom, 0.75)]
    [InlineData(PortEdge.Left,   0.3)]
    public void Roundtrip_for_canonical_anchors(PortEdge edge, double f)
    {
        var a = new PortAnchor(edge, f);
        var p = Shape.GetEdgePoint(a, 200, 100);
        var back = Shape.InferAnchor(p, 200, 100);
        Assert.Equal(a, back);
    }

    [Fact]
    public void Shared_corner_canonicalizes_to_next_edge_start()
    {
        var pTop1   = Shape.GetEdgePoint(new PortAnchor(PortEdge.Top,   1.0), 200, 100);
        var pRight0 = Shape.GetEdgePoint(new PortAnchor(PortEdge.Right, 0.0), 200, 100);
        Assert.Equal(pTop1.X, pRight0.X, 9);
        Assert.Equal(pTop1.Y, pRight0.Y, 9);

        var canonical = Shape.InferAnchor(pTop1, 200, 100);
        Assert.Equal(PortEdge.Right, canonical.Edge);
        Assert.Equal(0.0, canonical.Fraction, 9);
    }

    [Fact]
    public void GetEdgePoint_at_zero_size_returns_origin()
    {
        var p = Shape.GetEdgePoint(PortAnchor.Right(0.5), 0, 0);
        Assert.Equal(0, p.X);
        Assert.Equal(0, p.Y);
    }

    [Theory]
    [InlineData(PortEdge.Left,   -1.0,  0.0)]
    [InlineData(PortEdge.Top,     0.0, -1.0)]
    [InlineData(PortEdge.Right,   1.0,  0.0)]
    [InlineData(PortEdge.Bottom,  0.0,  1.0)]
    public void GetEdgeOutwardNormal_at_zero_size_returns_cardinal(PortEdge edge, double nx, double ny)
    {
        var n = Shape.GetEdgeOutwardNormal(new PortAnchor(edge, 0.5), 0, 0);
        Assert.Equal(nx, n.X);
        Assert.Equal(ny, n.Y);
    }

    [Fact]
    public void InferAnchor_at_zero_size_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Shape.InferAnchor(new Point(0, 0), 0, 0));
    }
}
