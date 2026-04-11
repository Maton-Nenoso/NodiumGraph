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
