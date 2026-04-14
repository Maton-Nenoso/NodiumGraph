using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class BezierRouterTests
{
    [Fact]
    public void Route_returns_four_points_for_bezier_curve()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));  // right edge
        var target = new Port(nodeB, new Point(0, 25));    // left edge

        var points = router.Route(source, target);

        Assert.Equal(4, points.Count);
        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[3]);
    }

    [Fact]
    public void Control_points_are_horizontally_offset()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));  // right edge
        var target = new Port(nodeB, new Point(0, 25));    // left edge

        var points = router.Route(source, target);

        Assert.Equal(points[0].Y, points[1].Y);
        Assert.Equal(points[3].Y, points[2].Y);
        Assert.True(points[1].X > points[0].X);
        Assert.True(points[2].X < points[3].X);
    }

    [Fact]
    public void Offset_scales_with_distance()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));  // right edge

        var nodeNear = new Node { X = 200, Y = 0, Width = 100, Height = 50 };
        var targetNear = new Port(nodeNear, new Point(0, 25));  // left edge

        var nodeFar = new Node { X = 600, Y = 0, Width = 100, Height = 50 };
        var targetFar = new Port(nodeFar, new Point(0, 25));    // left edge

        var nearPoints = router.Route(source, targetNear);
        var farPoints = router.Route(source, targetFar);

        var nearOffset = nearPoints[1].X - nearPoints[0].X;
        var farOffset = farPoints[1].X - farPoints[0].X;

        Assert.True(farOffset > nearOffset);
    }

    [Fact]
    public void Route_right_to_left_does_not_cross()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 300, Y = 0, Width = 100, Height = 100 };
        var nodeB = new Node { X = 100, Y = 0, Width = 100, Height = 100 };
        var source = new Port(nodeA, new Point(0, 50));    // left edge of nodeA
        var target = new Port(nodeB, new Point(100, 50));  // right edge of nodeB

        var points = router.Route(source, target);

        var cp1 = points[1];
        var cp2 = points[2];

        Assert.True(cp1.X <= points[0].X,
            $"cp1.X ({cp1.X}) should be <= start.X ({points[0].X}) for right-to-left connection");
        Assert.True(cp2.X >= points[3].X,
            $"cp2.X ({cp2.X}) should be >= end.X ({points[3].X}) for right-to-left connection");
    }
}
