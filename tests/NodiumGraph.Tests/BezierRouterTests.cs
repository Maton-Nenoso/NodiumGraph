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
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        Assert.Equal(4, points.Count);
        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[3]);
    }

    [Fact]
    public void Control_points_are_horizontally_offset()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        // Control points should have same Y as their anchor but offset X
        Assert.Equal(points[0].Y, points[1].Y);
        Assert.Equal(points[3].Y, points[2].Y);
        Assert.True(points[1].X > points[0].X);
        Assert.True(points[2].X < points[3].X);
    }

    [Fact]
    public void Offset_scales_with_distance()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var source = new Port(nodeA, new Point(0, 0));

        var nodeNear = new Node { X = 100, Y = 0 };
        var targetNear = new Port(nodeNear, new Point(0, 0));

        var nodeFar = new Node { X = 500, Y = 0 };
        var targetFar = new Port(nodeFar, new Point(0, 0));

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
        var nodeA = new Node { X = 300, Y = 0 };
        var nodeB = new Node { X = 100, Y = 0 };
        var source = new Port(nodeA, new Point(0, 50));  // AbsolutePosition = (300, 50)
        var target = new Port(nodeB, new Point(0, 50));  // AbsolutePosition = (100, 50)

        var points = router.Route(source, target);

        var cp1 = points[1];
        var cp2 = points[2];

        // For right-to-left: cp1 should be pushed LEFT (toward target), so cp1.X <= start.X
        Assert.True(cp1.X <= points[0].X,
            $"cp1.X ({cp1.X}) should be <= start.X ({points[0].X}) for right-to-left connection");
        // cp2 should be pushed RIGHT (toward source), so cp2.X >= end.X
        Assert.True(cp2.X >= points[3].X,
            $"cp2.X ({cp2.X}) should be >= end.X ({points[3].X}) for right-to-left connection");
    }
}
