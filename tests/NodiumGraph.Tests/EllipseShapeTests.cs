using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class EllipseShapeTests
{
    private const double Tolerance = 0.001;
    private readonly EllipseShape _shape = new();

    // ---- Points on/outside the ellipse: boundary at angle toward position ----

    [Fact]
    public void Above_center_returns_top_of_ellipse()
    {
        // Position directly above → angle = -90° (up) → boundary at (0, -b)
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
        // Point inside ellipse → project in the same angular direction
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
}
