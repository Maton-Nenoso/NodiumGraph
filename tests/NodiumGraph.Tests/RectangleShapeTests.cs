using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class RectangleShapeTests
{
    private const double Tolerance = 0.001;
    private readonly RectangleShape _shape = new();

    // ---- Outside: clamps to nearest boundary ----

    [Fact]
    public void Outside_above_snaps_to_top_edge()
    {
        // Point directly above center → top edge center
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, -100), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_below_snaps_to_bottom_edge()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, 100), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_left_snaps_to_left_edge()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(-200, 0), 100, 80);
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_right_snaps_to_right_edge()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(200, 0), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_upper_right_diagonal_clamps_to_corner()
    {
        // Point diagonally outside upper-right corner → clamp to (50, -40)
        var pt = _shape.GetNearestBoundaryPoint(new Point(200, -200), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Outside_lower_left_diagonal_clamps_to_corner()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(-200, 200), 100, 80);
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    // ---- Inside: snaps to nearest edge ----

    [Fact]
    public void Inside_point_near_right_snaps_to_right_edge()
    {
        // (45, 0) inside a 100x80 rect — nearest edge is right (dist = 5)
        var pt = _shape.GetNearestBoundaryPoint(new Point(45, 0), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Inside_point_near_top_snaps_to_top_edge()
    {
        // (0, -38) inside a 100x80 rect — nearest edge is top (dist = 2)
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, -38), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(-40, pt.Y, Tolerance);
    }

    [Fact]
    public void Inside_point_near_bottom_snaps_to_bottom_edge()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, 38), 100, 80);
        Assert.Equal(0, pt.X, Tolerance);
        Assert.Equal(40, pt.Y, Tolerance);
    }

    [Fact]
    public void Inside_point_near_left_snaps_to_left_edge()
    {
        var pt = _shape.GetNearestBoundaryPoint(new Point(-48, 0), 100, 80);
        Assert.Equal(-50, pt.X, Tolerance);
        Assert.Equal(0, pt.Y, Tolerance);
    }

    [Fact]
    public void Center_snaps_to_an_edge()
    {
        // Center (0, 0) of a 100x80 rect — nearest edges are top and bottom (both at 40)
        // The implementation picks one — just verify result is ON an edge
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, 0), 100, 80);
        var onRightOrLeft = Math.Abs(Math.Abs(pt.X) - 50) < Tolerance;
        var onTopOrBottom = Math.Abs(Math.Abs(pt.Y) - 40) < Tolerance;
        Assert.True(onRightOrLeft || onTopOrBottom);
    }

    [Fact]
    public void Center_of_square_snaps_to_an_edge()
    {
        // For a square, all 4 edges are equidistant — result must be on one of them
        var pt = _shape.GetNearestBoundaryPoint(new Point(0, 0), 100, 100);
        var onRightOrLeft = Math.Abs(Math.Abs(pt.X) - 50) < Tolerance;
        var onTopOrBottom = Math.Abs(Math.Abs(pt.Y) - 50) < Tolerance;
        Assert.True(onRightOrLeft || onTopOrBottom);
    }

    // ---- On the boundary: returns same point ----

    [Fact]
    public void On_boundary_returns_same_point()
    {
        // A point exactly on the right edge
        var pt = _shape.GetNearestBoundaryPoint(new Point(50, 10), 100, 80);
        Assert.Equal(50, pt.X, Tolerance);
        Assert.Equal(10, pt.Y, Tolerance);
    }
}
