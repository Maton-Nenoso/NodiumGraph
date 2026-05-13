using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class RoundedRectangleShapeTests
{
    private const double Tolerance = 0.001;

    // ---- Straight edge regions (same as rectangle) ----

    [Fact]
    public void Outside_above_center_snaps_to_top_edge()
    {
        var shape = new RoundedRectangleShape(10);
        // Directly above → top edge center (not in a corner region)
        var pt = shape.GetNearestBoundaryPoint(new Point(0, -100), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_right_of_center_snaps_to_right_edge()
    {
        var shape = new RoundedRectangleShape(10);
        var pt = shape.GetNearestBoundaryPoint(new Point(200, 0), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_below_center_snaps_to_bottom_edge()
    {
        var shape = new RoundedRectangleShape(10);
        var pt = shape.GetNearestBoundaryPoint(new Point(0, 200), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_left_of_center_snaps_to_left_edge()
    {
        var shape = new RoundedRectangleShape(10);
        var pt = shape.GetNearestBoundaryPoint(new Point(-200, 0), 100, 80);
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    // ---- Corner region: result lies on the arc ----

    [Fact]
    public void Corner_point_lies_on_arc_upper_right()
    {
        var shape = new RoundedRectangleShape(15);
        // halfW=50, halfH=40, r=15 → innerHalf=(35,25), corner center at (35,25)
        // Position diagonally outside upper-right corner
        var pt = shape.GetNearestBoundaryPoint(new Point(200, -200), 100, 80);
        var cx = 35.0; // innerHalfW
        var cy = -25.0; // -innerHalfH (upper-right = positive X, negative Y)
        var dist = Math.Sqrt((pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy));
        Assert.Equal(15.0, dist, Tolerance);
    }

    [Fact]
    public void Corner_point_lies_on_arc_lower_left()
    {
        var shape = new RoundedRectangleShape(15);
        var pt = shape.GetNearestBoundaryPoint(new Point(-200, 200), 100, 80);
        var cx = -35.0;
        var cy = 25.0;
        var dist = Math.Sqrt((pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy));
        Assert.Equal(15.0, dist, Tolerance);
    }

    [Fact]
    public void Corner_point_within_outer_boundary()
    {
        var shape = new RoundedRectangleShape(15);
        var pt = shape.GetNearestBoundaryPoint(new Point(100, -100), 100, 80);
        Assert.True(Math.Abs(pt.X) <= 50 + Tolerance);
        Assert.True(Math.Abs(pt.Y) <= 40 + Tolerance);
    }

    // ---- Zero radius degenerates to plain rectangle ----

    [Fact]
    public void Zero_radius_matches_rectangle()
    {
        var rounded = new RoundedRectangleShape(0);
        var rect = new RectangleShape();

        foreach (var pos in new[] {
            new Point(0, -100), new Point(200, 0), new Point(0, 100),
            new Point(-200, 0), new Point(150, -150), new Point(10, 5)
        })
        {
            var rp = rounded.GetNearestBoundaryPoint(pos, 100, 80);
            var rr = rect.GetNearestBoundaryPoint(pos, 100, 80);
            Assert.Equal(rr.X, rp.X, Tolerance);
            Assert.Equal(rr.Y, rp.Y, Tolerance);
        }
    }

    // ---- Rounded rect corner is inside rectangle boundary ----

    [Fact]
    public void Corner_boundary_point_inside_rectangle_boundary()
    {
        var rounded = new RoundedRectangleShape(15);
        var rect = new RectangleShape();

        // Diagonal position hits corner on both shapes — rounded should be closer to center
        var pos = new Point(200, -200);
        var rp = rounded.GetNearestBoundaryPoint(pos, 100, 80);
        var rr = rect.GetNearestBoundaryPoint(pos, 100, 80);

        var distRounded = Math.Sqrt(rp.X * rp.X + rp.Y * rp.Y);
        var distRect = Math.Sqrt(rr.X * rr.X + rr.Y * rr.Y);
        Assert.True(distRounded <= distRect + Tolerance);
    }

    // ---- Large radius is clamped ----

    [Fact]
    public void Large_radius_clamped_result_within_bounds()
    {
        var shape = new RoundedRectangleShape(100); // clamped to min(50,40)=40
        var pt = shape.GetNearestBoundaryPoint(new Point(200, -200), 100, 80);
        Assert.True(Math.Abs(pt.X) <= 50 + Tolerance);
        Assert.True(Math.Abs(pt.Y) <= 40 + Tolerance);
    }

    // ---- Negative radius throws ----

    [Fact]
    public void Negative_radius_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoundedRectangleShape(-1));
    }

    // ---- Inside points ----

    [Fact]
    public void Inside_near_right_snaps_to_right_edge_area()
    {
        var shape = new RoundedRectangleShape(10);
        // (45, 0) near right edge — not in corner, behaves like rectangle
        var pt = shape.GetNearestBoundaryPoint(new Point(45, 0), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }
}

// ---- Anchor-based tests ----

public class RoundedRectangleShapeAnchorTests
{
    private const double Tol = 0.001;
    // Normal RRect: w=200, h=100, r=20 → rEff=20
    private static readonly RoundedRectangleShape NormalShape = new(20);

    // ---- GetEdgePoint acceptance criteria ----

    [Fact]
    public void GetEdgePoint_Top0_is_TL_corner_midpoint()
    {
        // TL corner center = (20, 20), angle = -3π/4
        // Expected: (20 + 20·cos(-3π/4), 20 + 20·sin(-3π/4)) = (20 - 14.142, 20 - 14.142) ≈ (5.858, 5.858)
        var p = NormalShape.GetEdgePoint(PortAnchor.Top(0.0), 200, 100);
        Assert.Equal(5.858, p.X, Tol);
        Assert.Equal(5.858, p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Top1_is_TR_corner_midpoint()
    {
        // TR corner center = (180, 20), angle = -π/4
        // Expected: (180 + 20·cos(-π/4), 20 + 20·sin(-π/4)) = (180 + 14.142, 20 - 14.142) ≈ (194.142, 5.858)
        var p = NormalShape.GetEdgePoint(PortAnchor.Top(1.0), 200, 100);
        Assert.Equal(194.142, p.X, Tol);
        Assert.Equal(5.858,   p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Top05_lands_in_flat_segment()
    {
        // f=0.5 is well within flat region (t1≈0.082, t2≈0.918)
        // u=(0.5-t1)/(t2-t1)=0.5 → lerp((20,0),(180,0),0.5)=(100,0)
        var p = NormalShape.GetEdgePoint(PortAnchor.Top(0.5), 200, 100);
        Assert.Equal(100.0, p.X, Tol);
        Assert.Equal(0.0,   p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Right0_is_TR_corner_midpoint()
    {
        // TR corner center = (180, 20), angle = -π/4
        // Right(0) = same as Top(1) corner point
        var pRight0 = NormalShape.GetEdgePoint(PortAnchor.Right(0.0), 200, 100);
        var pTop1   = NormalShape.GetEdgePoint(PortAnchor.Top(1.0),   200, 100);
        Assert.Equal(pTop1.X, pRight0.X, Tol);
        Assert.Equal(pTop1.Y, pRight0.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Right05_lands_in_flat_segment()
    {
        // Right flat: x=200, y in [20,80]. Midpoint = (200, 50).
        var p = NormalShape.GetEdgePoint(PortAnchor.Right(0.5), 200, 100);
        Assert.Equal(200.0, p.X, Tol);
        Assert.Equal(50.0,  p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Bottom0_is_BR_corner_midpoint()
    {
        // BR corner center = (180, 80), angle = π/4
        // Expected: (180 + 20·cos(π/4), 80 + 20·sin(π/4)) ≈ (194.142, 94.142)
        var p = NormalShape.GetEdgePoint(PortAnchor.Bottom(0.0), 200, 100);
        Assert.Equal(194.142, p.X, Tol);
        Assert.Equal(94.142,  p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Bottom05_lands_in_flat_segment()
    {
        // Bottom flat: y=100, x from (180,100) to (20,100). Midpoint x = 100.
        var p = NormalShape.GetEdgePoint(PortAnchor.Bottom(0.5), 200, 100);
        Assert.Equal(100.0, p.X, Tol);
        Assert.Equal(100.0, p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Left0_is_BL_corner_midpoint()
    {
        // BL corner center = (20, 80), angle = 3π/4
        // Expected: (20 + 20·cos(3π/4), 80 + 20·sin(3π/4)) = (20 - 14.142, 80 + 14.142) ≈ (5.858, 94.142)
        var p = NormalShape.GetEdgePoint(PortAnchor.Left(0.0), 200, 100);
        Assert.Equal(5.858,  p.X, Tol);
        Assert.Equal(94.142, p.Y, Tol);
    }

    [Fact]
    public void GetEdgePoint_Left05_lands_in_flat_segment()
    {
        // Left flat: x=0, y from (0,80) to (0,20). Midpoint y = 50.
        var p = NormalShape.GetEdgePoint(PortAnchor.Left(0.5), 200, 100);
        Assert.Equal(0.0,  p.X, Tol);
        Assert.Equal(50.0, p.Y, Tol);
    }

    // All four edges: adjacent-edge corner points must coincide (shared corners)
    [Fact]
    public void Shared_corners_coincide_across_edges()
    {
        var pTop1    = NormalShape.GetEdgePoint(PortAnchor.Top(1.0),    200, 100);
        var pRight0  = NormalShape.GetEdgePoint(PortAnchor.Right(0.0),  200, 100);
        var pRight1  = NormalShape.GetEdgePoint(PortAnchor.Right(1.0),  200, 100);
        var pBottom0 = NormalShape.GetEdgePoint(PortAnchor.Bottom(0.0), 200, 100);
        var pBottom1 = NormalShape.GetEdgePoint(PortAnchor.Bottom(1.0), 200, 100);
        var pLeft0   = NormalShape.GetEdgePoint(PortAnchor.Left(0.0),   200, 100);
        var pLeft1   = NormalShape.GetEdgePoint(PortAnchor.Left(1.0),   200, 100);
        var pTop0    = NormalShape.GetEdgePoint(PortAnchor.Top(0.0),    200, 100);

        Assert.Equal(pTop1.X,    pRight0.X,  Tol);
        Assert.Equal(pTop1.Y,    pRight0.Y,  Tol);
        Assert.Equal(pRight1.X,  pBottom0.X, Tol);
        Assert.Equal(pRight1.Y,  pBottom0.Y, Tol);
        Assert.Equal(pBottom1.X, pLeft0.X,   Tol);
        Assert.Equal(pBottom1.Y, pLeft0.Y,   Tol);
        Assert.Equal(pLeft1.X,   pTop0.X,    Tol);
        Assert.Equal(pLeft1.Y,   pTop0.Y,    Tol);
    }

    // ---- GetEdgeOutwardNormal ----

    [Theory]
    [InlineData(PortEdge.Top,     0.5,  0.0, -1.0)]
    [InlineData(PortEdge.Right,   0.5,  1.0,  0.0)]
    [InlineData(PortEdge.Bottom,  0.5,  0.0,  1.0)]
    [InlineData(PortEdge.Left,    0.5, -1.0,  0.0)]
    public void GetEdgeOutwardNormal_flat_region_is_cardinal(PortEdge edge, double f, double nx, double ny)
    {
        var n = NormalShape.GetEdgeOutwardNormal(new PortAnchor(edge, f), 200, 100);
        Assert.Equal(nx, n.X, Tol);
        Assert.Equal(ny, n.Y, Tol);
    }

    [Fact]
    public void GetEdgeOutwardNormal_at_corner_midpoint_is_radial()
    {
        // Top(0) = TL corner midpoint, angle = -3π/4
        // Normal = (cos(-3π/4), sin(-3π/4)) = (-0.7071, -0.7071)
        var n = NormalShape.GetEdgeOutwardNormal(PortAnchor.Top(0.0), 200, 100);
        Assert.Equal(-0.7071, n.X, Tol);
        Assert.Equal(-0.7071, n.Y, Tol);
    }

    [Fact]
    public void GetEdgeOutwardNormal_is_unit_vector_on_arc()
    {
        // At any arc point, the outward normal should be a unit vector
        var n = NormalShape.GetEdgeOutwardNormal(PortAnchor.Right(0.0), 200, 100);
        var mag = Math.Sqrt(n.X * n.X + n.Y * n.Y);
        Assert.Equal(1.0, mag, Tol);
    }

    [Theory]
    [InlineData(PortEdge.Left,   -1.0,  0.0)]
    [InlineData(PortEdge.Top,     0.0, -1.0)]
    [InlineData(PortEdge.Right,   1.0,  0.0)]
    [InlineData(PortEdge.Bottom,  0.0,  1.0)]
    public void GetEdgeOutwardNormal_zero_size_returns_cardinal(PortEdge edge, double nx, double ny)
    {
        var n = NormalShape.GetEdgeOutwardNormal(new PortAnchor(edge, 0.5), 0, 0);
        Assert.Equal(nx, n.X);
        Assert.Equal(ny, n.Y);
    }

    // ---- GetEdgePoint zero-dim ----

    [Fact]
    public void GetEdgePoint_zero_size_returns_origin()
    {
        var p = NormalShape.GetEdgePoint(PortAnchor.Right(0.5), 0, 0);
        Assert.Equal(0, p.X);
        Assert.Equal(0, p.Y);
    }

    // ---- InferAnchor zero-dim ----

    [Fact]
    public void InferAnchor_zero_size_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            NormalShape.InferAnchor(new Point(0, 0), 0, 0));
    }

    // ---- Round-trip: GetEdgePoint → InferAnchor ----

    [Theory]
    [InlineData(PortEdge.Top,    0.05)]   // in leading arc
    [InlineData(PortEdge.Top,    0.25)]   // in flat
    [InlineData(PortEdge.Top,    0.5)]    // flat midpoint
    [InlineData(PortEdge.Top,    0.75)]   // in flat
    [InlineData(PortEdge.Top,    0.95)]   // in trailing arc
    [InlineData(PortEdge.Right,  0.05)]
    [InlineData(PortEdge.Right,  0.5)]
    [InlineData(PortEdge.Right,  0.95)]
    [InlineData(PortEdge.Bottom, 0.05)]
    [InlineData(PortEdge.Bottom, 0.5)]
    [InlineData(PortEdge.Bottom, 0.95)]
    [InlineData(PortEdge.Left,   0.05)]
    [InlineData(PortEdge.Left,   0.5)]
    [InlineData(PortEdge.Left,   0.95)]
    public void Roundtrip_canonical_anchors(PortEdge edge, double f)
    {
        var anchor = new PortAnchor(edge, f);
        var p = NormalShape.GetEdgePoint(anchor, 200, 100);
        var back = NormalShape.InferAnchor(p, 200, 100);
        Assert.Equal(anchor.Edge, back.Edge);
        Assert.Equal(anchor.Fraction, back.Fraction, Tol);
    }

    // ---- Shared-corner canonicalization ----

    [Fact]
    public void SharedCorner_Top1_canonicalizes_to_Right0()
    {
        var p = NormalShape.GetEdgePoint(PortAnchor.Top(1.0), 200, 100);
        var canonical = NormalShape.InferAnchor(p, 200, 100);
        Assert.Equal(PortEdge.Right, canonical.Edge);
        Assert.Equal(0.0, canonical.Fraction, Tol);
    }

    [Fact]
    public void SharedCorner_Right1_canonicalizes_to_Bottom0()
    {
        var p = NormalShape.GetEdgePoint(PortAnchor.Right(1.0), 200, 100);
        var canonical = NormalShape.InferAnchor(p, 200, 100);
        Assert.Equal(PortEdge.Bottom, canonical.Edge);
        Assert.Equal(0.0, canonical.Fraction, Tol);
    }

    [Fact]
    public void SharedCorner_Bottom1_canonicalizes_to_Left0()
    {
        var p = NormalShape.GetEdgePoint(PortAnchor.Bottom(1.0), 200, 100);
        var canonical = NormalShape.InferAnchor(p, 200, 100);
        Assert.Equal(PortEdge.Left, canonical.Edge);
        Assert.Equal(0.0, canonical.Fraction, Tol);
    }

    [Fact]
    public void SharedCorner_Left1_canonicalizes_to_Top0()
    {
        var p = NormalShape.GetEdgePoint(PortAnchor.Left(1.0), 200, 100);
        var canonical = NormalShape.InferAnchor(p, 200, 100);
        Assert.Equal(PortEdge.Top, canonical.Edge);
        Assert.Equal(0.0, canonical.Fraction, Tol);
    }

    // ---- Horizontal capsule: w=200, h=100, r=50 ----
    // rEff = min(50, min(100, 50)) = 50
    // Left/Right: flat=0 (capsule). Top/Bottom: flat=100.

    [Fact]
    public void Capsule_Left_midpoint_is_left_center()
    {
        var shape = new RoundedRectangleShape(50);
        // Left(0.5) = midpoint of left edge = (0, 50)
        var p = shape.GetEdgePoint(PortAnchor.Left(0.5), 200, 100);
        Assert.Equal(0.0,  p.X, Tol);
        Assert.Equal(50.0, p.Y, Tol);
    }

    [Fact]
    public void Capsule_Right_midpoint_is_right_center()
    {
        var shape = new RoundedRectangleShape(50);
        var p = shape.GetEdgePoint(PortAnchor.Right(0.5), 200, 100);
        Assert.Equal(200.0, p.X, Tol);
        Assert.Equal(50.0,  p.Y, Tol);
    }

    [Fact]
    public void Capsule_Top_still_has_flat_segment()
    {
        var shape = new RoundedRectangleShape(50);
        // Top flat = w - 2*rEff = 200 - 100 = 100. Midpoint = (100, 0).
        var p = shape.GetEdgePoint(PortAnchor.Top(0.5), 200, 100);
        Assert.Equal(100.0, p.X, Tol);
        Assert.Equal(0.0,   p.Y, Tol);
    }

    [Fact]
    public void Capsule_Left_edge_roundtrip()
    {
        var shape = new RoundedRectangleShape(50);
        foreach (var f in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
        {
            var anchor = PortAnchor.Left(f);
            var p = shape.GetEdgePoint(anchor, 200, 100);
            var back = shape.InferAnchor(p, 200, 100);
            Assert.Equal(anchor.Edge, back.Edge);
            Assert.Equal(anchor.Fraction, back.Fraction, Tol);
        }
    }

    // ---- Square + large radius (all capsule edges): w=h=100, r=60 ----
    // rEff = min(60, min(50, 50)) = 50; all flats = 0.

    [Fact]
    public void SquareCapsule_Top05_is_top_center_of_inscribed_circle()
    {
        var shape = new RoundedRectangleShape(60);
        // All edges fully arc (zero flat). Top(0.5) = top of circle = (50, 0).
        var p = shape.GetEdgePoint(PortAnchor.Top(0.5), 100, 100);
        Assert.Equal(50.0, p.X, Tol);
        Assert.Equal(0.0,  p.Y, Tol);
    }

    [Fact]
    public void SquareCapsule_Right05_is_right_center_of_inscribed_circle()
    {
        var shape = new RoundedRectangleShape(60);
        var p = shape.GetEdgePoint(PortAnchor.Right(0.5), 100, 100);
        Assert.Equal(100.0, p.X, Tol);
        Assert.Equal(50.0,  p.Y, Tol);
    }

    [Fact]
    public void SquareCapsule_roundtrip()
    {
        var shape = new RoundedRectangleShape(60);
        foreach (var (edge, f) in new[] {
            (PortEdge.Top, 0.1), (PortEdge.Top, 0.5), (PortEdge.Top, 0.9),
            (PortEdge.Right, 0.3), (PortEdge.Bottom, 0.7), (PortEdge.Left, 0.2)
        })
        {
            var anchor = new PortAnchor(edge, f);
            var p = shape.GetEdgePoint(anchor, 100, 100);
            var back = shape.InferAnchor(p, 100, 100);
            Assert.Equal(anchor.Edge, back.Edge);
            Assert.Equal(anchor.Fraction, back.Fraction, Tol);
        }
    }

    // ---- rEff=0 falls back to rectangle-like behavior ----

    [Fact]
    public void ZeroRadius_GetEdgePoint_flat_midpoints()
    {
        var shape = new RoundedRectangleShape(0);
        // With rEff=0, flat_top = w, halfArc=0, total=w.
        // t1=t2=0.5 (capsule path), but flat>0... actually:
        // flat=200, halfArc=0, total=200; flat>0 so t1=0/200=0, t2=200/200=1.
        // Top(0.5): u=0.5, flat region, lerp((0,0),(200,0),0.5)=(100,0) ✓
        var p = shape.GetEdgePoint(PortAnchor.Top(0.5), 200, 100);
        Assert.Equal(100.0, p.X, Tol);
        Assert.Equal(0.0,   p.Y, Tol);
    }

    // ---- Specific arc correctness: verify points lie on the expected circle ----

    [Fact]
    public void ArcPoints_lie_on_corner_circle()
    {
        // For w=200, h=100, r=20: rEff=20
        // Any point in the arc region should be exactly rEff from the corner center.
        var shape = new RoundedRectangleShape(20);
        double w = 200, h = 100;
        double rEff = 20;

        // TL corner center
        double tlCx = rEff, tlCy = rEff;
        // TR corner center
        double trCx = w - rEff, trCy = rEff;
        // BR corner center
        double brCx = w - rEff, brCy = h - rEff;
        // BL corner center
        double blCx = rEff, blCy = h - rEff;

        // Test arc points from each edge's leading/trailing arc
        // Top leading arc (f ≈ 0.04 → in leading arc region)
        var pTopLead = shape.GetEdgePoint(new PortAnchor(PortEdge.Top, 0.04), w, h);
        Assert.Equal(rEff, Math.Sqrt(Math.Pow(pTopLead.X - tlCx, 2) + Math.Pow(pTopLead.Y - tlCy, 2)), Tol);

        // Top trailing arc (f ≈ 0.96 → in trailing arc)
        var pTopTrail = shape.GetEdgePoint(new PortAnchor(PortEdge.Top, 0.96), w, h);
        Assert.Equal(rEff, Math.Sqrt(Math.Pow(pTopTrail.X - trCx, 2) + Math.Pow(pTopTrail.Y - trCy, 2)), Tol);

        // Bottom leading arc (f ≈ 0.04)
        var pBotLead = shape.GetEdgePoint(new PortAnchor(PortEdge.Bottom, 0.04), w, h);
        Assert.Equal(rEff, Math.Sqrt(Math.Pow(pBotLead.X - brCx, 2) + Math.Pow(pBotLead.Y - brCy, 2)), Tol);

        // Left arc (f ≈ 0.04)
        var pLeftLead = shape.GetEdgePoint(new PortAnchor(PortEdge.Left, 0.04), w, h);
        Assert.Equal(rEff, Math.Sqrt(Math.Pow(pLeftLead.X - blCx, 2) + Math.Pow(pLeftLead.Y - blCy, 2)), Tol);
    }

    // ---- Degenerate: very small shape ----

    [Fact]
    public void VerySmallShape_does_not_throw()
    {
        var shape = new RoundedRectangleShape(20);
        // w=10, h=10 → rEff=5; shape has arcs but no flat
        var p = shape.GetEdgePoint(PortAnchor.Top(0.5), 10, 10);
        // Top(0.5) = top center = (5, 0)
        Assert.Equal(5.0, p.X, Tol);
        Assert.Equal(0.0, p.Y, Tol);
    }
}
