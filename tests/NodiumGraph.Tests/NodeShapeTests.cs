using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeShapeTests
{
    private const double Tolerance = 0.01;

    // ---- RectangleShape tests ----

    [Fact]
    public void Rectangle_0_degrees_returns_top_center()
    {
        var shape = new RectangleShape();
        var pt = shape.GetBoundaryPoint(0, 100, 80);
        // 0 degrees = top center = (0, -40)
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Rectangle_90_degrees_returns_right_center()
    {
        var shape = new RectangleShape();
        var pt = shape.GetBoundaryPoint(90, 100, 80);
        // 90 degrees = right center = (50, 0)
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Rectangle_180_degrees_returns_bottom_center()
    {
        var shape = new RectangleShape();
        var pt = shape.GetBoundaryPoint(180, 100, 80);
        // 180 degrees = bottom center = (0, 40)
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    [Fact]
    public void Rectangle_270_degrees_returns_left_center()
    {
        var shape = new RectangleShape();
        var pt = shape.GetBoundaryPoint(270, 100, 80);
        // 270 degrees = left center = (-50, 0)
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Rectangle_45_degrees_hits_corner_region()
    {
        var shape = new RectangleShape();
        // 45 degrees on a 100x80 rect: direction is (sin(45), -cos(45)) = (0.707, -0.707)
        // tx = 50/0.707 = 70.71, ty = 40/0.707 = 56.57 -> min is ty = 56.57
        // Point = (56.57 * 0.707, 56.57 * -0.707) = (40, -40) -> hits top edge
        var pt = shape.GetBoundaryPoint(45, 100, 80);
        Assert.Equal(40, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Rectangle_square_45_degrees_hits_corner()
    {
        var shape = new RectangleShape();
        // 45 degrees on a 100x100 square: tx = ty = 50/0.707 = 70.71
        // Point = (70.71 * 0.707, 70.71 * -0.707) = (50, -50)
        var pt = shape.GetBoundaryPoint(45, 100, 100);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(-50, pt.Y, Tolerance);
    }

    // ---- EllipseShape tests ----

    [Fact]
    public void Ellipse_0_degrees_returns_top()
    {
        var shape = new EllipseShape();
        var pt = shape.GetBoundaryPoint(0, 100, 80);
        // x = 50*sin(0) = 0, y = -40*cos(0) = -40
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Ellipse_90_degrees_returns_right()
    {
        var shape = new EllipseShape();
        var pt = shape.GetBoundaryPoint(90, 100, 80);
        // x = 50*sin(90) = 50, y = -40*cos(90) = 0
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Ellipse_180_degrees_returns_bottom()
    {
        var shape = new EllipseShape();
        var pt = shape.GetBoundaryPoint(180, 100, 80);
        // x = 50*sin(180) = 0, y = -40*cos(180) = 40
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    [Fact]
    public void Ellipse_270_degrees_returns_left()
    {
        var shape = new EllipseShape();
        var pt = shape.GetBoundaryPoint(270, 100, 80);
        // x = 50*sin(270) = -50, y = -40*cos(270) = 0
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Ellipse_point_lies_on_ellipse_boundary()
    {
        var shape = new EllipseShape();
        var pt = shape.GetBoundaryPoint(30, 200, 100);
        // Verify point is on ellipse: (x/a)^2 + (y/b)^2 = 1
        var a = 100.0; // width/2
        var b = 50.0;  // height/2
        var value = (pt.X / a) * (pt.X / a) + (pt.Y / b) * (pt.Y / b);
        Assert.Equal(1.0, value, Tolerance);
    }

    [Fact]
    public void Ellipse_circle_all_points_at_same_distance()
    {
        var shape = new EllipseShape();
        var r = 60.0; // radius for 120x120 circle
        for (var angle = 0.0; angle < 360; angle += 30)
        {
            var pt = shape.GetBoundaryPoint(angle, 120, 120);
            var dist = Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y);
            Assert.Equal(r, dist, Tolerance);
        }
    }

    // ---- RoundedRectangleShape tests ----

    [Fact]
    public void RoundedRect_0_degrees_returns_top_center()
    {
        var shape = new RoundedRectangleShape(10);
        var pt = shape.GetBoundaryPoint(0, 100, 80);
        // Top center is a straight edge, not corner -> same as rectangle
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void RoundedRect_90_degrees_returns_right_center()
    {
        var shape = new RoundedRectangleShape(10);
        var pt = shape.GetBoundaryPoint(90, 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void RoundedRect_zero_radius_matches_rectangle()
    {
        var rounded = new RoundedRectangleShape(0);
        var rect = new RectangleShape();

        for (var angle = 0.0; angle < 360; angle += 15)
        {
            var rp = rounded.GetBoundaryPoint(angle, 100, 80);
            var rr = rect.GetBoundaryPoint(angle, 100, 80);
            Assert.Equal(rr.X, rp.X, Tolerance);
            Assert.Equal(rr.Y, rp.Y, Tolerance);
        }
    }

    [Fact]
    public void RoundedRect_corner_point_inside_rectangle_boundary()
    {
        var rounded = new RoundedRectangleShape(15);
        var rect = new RectangleShape();

        // 45 degrees goes through the corner area
        var rp = rounded.GetBoundaryPoint(45, 100, 80);
        var rr = rect.GetBoundaryPoint(45, 100, 80);

        // The rounded rect point should be closer to center (or at most equal)
        var distRounded = Math.Sqrt(rp.X * rp.X + rp.Y * rp.Y);
        var distRect = Math.Sqrt(rr.X * rr.X + rr.Y * rr.Y);
        Assert.True(distRounded <= distRect + Tolerance);
    }

    [Fact]
    public void RoundedRect_max_radius_gives_ellipse_like_result()
    {
        // When corner radius = min(halfW, halfH), the shape becomes a stadium/capsule
        var shape = new RoundedRectangleShape(40); // halfH = 40, so r=40
        var pt = shape.GetBoundaryPoint(45, 100, 80);
        // Point should be well-defined and within bounds
        Assert.True(Math.Abs(pt.X) <= 50 + Tolerance);
        Assert.True(Math.Abs(pt.Y) <= 40 + Tolerance);
    }

    [Fact]
    public void RoundedRect_negative_radius_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoundedRectangleShape(-1));
    }

    [Fact]
    public void RoundedRect_large_radius_clamped_to_half_dimension()
    {
        // Corner radius 100 > min(25, 40) = 25, should be clamped
        var shape = new RoundedRectangleShape(100);
        // Should not throw, result should be valid
        var pt = shape.GetBoundaryPoint(45, 50, 80);
        Assert.True(Math.Abs(pt.X) <= 25 + Tolerance);
        Assert.True(Math.Abs(pt.Y) <= 40 + Tolerance);
    }

    // ---- Cross-shape consistency ----

    [Fact]
    public void All_shapes_agree_on_cardinal_directions_for_square()
    {
        // For a square, all shapes should agree on cardinal directions
        // (rectangle and rounded rectangle match exactly; ellipse matches at 0,90,180,270)
        var rect = new RectangleShape();
        var ellipse = new EllipseShape();
        var rounded = new RoundedRectangleShape(10);

        foreach (var angle in new[] { 0.0, 90.0, 180.0, 270.0 })
        {
            var rp = rect.GetBoundaryPoint(angle, 100, 100);
            var ep = ellipse.GetBoundaryPoint(angle, 100, 100);
            var rrp = rounded.GetBoundaryPoint(angle, 100, 100);

            Assert.Equal(rp.X, ep.X, Tolerance);
            Assert.Equal(rp.Y, ep.Y, Tolerance);
            Assert.Equal(rp.X, rrp.X, Tolerance);
            Assert.Equal(rp.Y, rrp.Y, Tolerance);
        }
    }
}
