using System;
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

public class RectangleShapeAnchorTests
{
    private static readonly RectangleShape Shape = new();
    private const double W = 100, H = 60;

    [Theory]
    [InlineData(PortEdge.Top,    0.0,   0.0,   0.0)]
    [InlineData(PortEdge.Top,    1.0, 100.0,   0.0)]
    [InlineData(PortEdge.Top,    0.5,  50.0,   0.0)]
    [InlineData(PortEdge.Right,  0.0, 100.0,   0.0)]
    [InlineData(PortEdge.Right,  1.0, 100.0,  60.0)]
    [InlineData(PortEdge.Right,  0.5, 100.0,  30.0)]
    [InlineData(PortEdge.Bottom, 0.0, 100.0,  60.0)]
    [InlineData(PortEdge.Bottom, 1.0,   0.0,  60.0)]
    [InlineData(PortEdge.Bottom, 0.5,  50.0,  60.0)]
    [InlineData(PortEdge.Left,   0.0,   0.0,  60.0)]
    [InlineData(PortEdge.Left,   1.0,   0.0,   0.0)]
    [InlineData(PortEdge.Left,   0.5,   0.0,  30.0)]
    public void GetEdgePoint_matches_per_edge_table(PortEdge edge, double f, double expectedX, double expectedY)
    {
        var p = Shape.GetEdgePoint(new PortAnchor(edge, f), W, H);
        Assert.Equal(expectedX, p.X, 9);
        Assert.Equal(expectedY, p.Y, 9);
    }

    [Theory]
    [InlineData(PortEdge.Left,   -1.0,  0.0)]
    [InlineData(PortEdge.Top,     0.0, -1.0)]
    [InlineData(PortEdge.Right,   1.0,  0.0)]
    [InlineData(PortEdge.Bottom,  0.0,  1.0)]
    public void GetEdgeOutwardNormal_is_cardinal_unit_vector(PortEdge edge, double nx, double ny)
    {
        var n = Shape.GetEdgeOutwardNormal(new PortAnchor(edge, 0.5), W, H);
        Assert.Equal(nx, n.X, 9);
        Assert.Equal(ny, n.Y, 9);
    }

    [Theory]
    [InlineData(  0.0,   0.0, PortEdge.Top,    0.0)]
    [InlineData(100.0,   0.0, PortEdge.Right,  0.0)]
    [InlineData(100.0,  60.0, PortEdge.Bottom, 0.0)]
    [InlineData(  0.0,  60.0, PortEdge.Left,   0.0)]
    public void InferAnchor_canonicalizes_corners(double x, double y, PortEdge expectedEdge, double expectedF)
    {
        var a = Shape.InferAnchor(new Point(x, y), W, H);
        Assert.Equal(expectedEdge, a.Edge);
        Assert.Equal(expectedF, a.Fraction, 9);
    }

    [Theory]
    [InlineData(PortEdge.Top,    0.25)]
    [InlineData(PortEdge.Top,    0.75)]
    [InlineData(PortEdge.Right,  0.5)]
    [InlineData(PortEdge.Bottom, 0.3)]
    [InlineData(PortEdge.Left,   0.8)]
    public void Roundtrip_for_canonical_anchors(PortEdge edge, double f)
    {
        var a = new PortAnchor(edge, f);
        var p = Shape.GetEdgePoint(a, W, H);
        var back = Shape.InferAnchor(p, W, H);
        Assert.Equal(a, back);
    }

    [Theory]
    [InlineData(PortEdge.Top,    PortEdge.Right,  0.0)]
    [InlineData(PortEdge.Right,  PortEdge.Bottom, 0.0)]
    [InlineData(PortEdge.Bottom, PortEdge.Left,   0.0)]
    [InlineData(PortEdge.Left,   PortEdge.Top,    0.0)]
    public void NonCanonical_Fraction1_canonicalizes_to_next_edge_zero(PortEdge fromEdge, PortEdge canonEdge, double canonF)
    {
        var nonCanon = new PortAnchor(fromEdge, 1.0);
        var p = Shape.GetEdgePoint(nonCanon, W, H);
        var inferred = Shape.InferAnchor(p, W, H);
        Assert.Equal(canonEdge, inferred.Edge);
        Assert.Equal(canonF, inferred.Fraction, 9);
        // Boundary point preserved either way:
        var p2 = Shape.GetEdgePoint(inferred, W, H);
        Assert.Equal(p.X, p2.X, 9);
        Assert.Equal(p.Y, p2.Y, 9);
    }

    [Fact]
    public void GetEdgePoint_at_zero_size_returns_origin()
    {
        var p = Shape.GetEdgePoint(PortAnchor.Right(0.5), 0, 0);
        Assert.Equal(0.0, p.X);
        Assert.Equal(0.0, p.Y);
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
