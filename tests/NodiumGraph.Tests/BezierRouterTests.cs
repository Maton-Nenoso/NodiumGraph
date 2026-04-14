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

    [Fact]
    public void Route_with_bottom_to_top_ports_pushes_control_points_vertically()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 0, Y = 200, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(50, 50));   // bottom edge of nodeA
        var target = new Port(nodeB, new Point(50, 0));    // top edge of nodeB

        var points = router.Route(source, target);
        var start = points[0];
        var cp1 = points[1];
        var cp2 = points[2];
        var end = points[3];

        Assert.Equal(start.X, cp1.X);
        Assert.True(cp1.Y > start.Y,
            $"cp1.Y ({cp1.Y}) should be > start.Y ({start.Y}) for a downward-emitting source");
        Assert.Equal(end.X, cp2.X);
        Assert.True(cp2.Y < end.Y,
            $"cp2.Y ({cp2.Y}) should be < end.Y ({end.Y}) for an upward-emitting target");
    }

    [Fact]
    public void Route_with_top_ports_on_same_y_produces_arc()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 200, Y = 0, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(50, 0));  // top edge
        var target = new Port(nodeB, new Point(50, 0));  // top edge

        var points = router.Route(source, target);
        var start = points[0];
        var cp1 = points[1];
        var cp2 = points[2];
        var end = points[3];

        // Both ports emit upward; dy == 0 so reach clamps to MinOffset (30).
        Assert.Equal(start.Y - 30, cp1.Y);
        Assert.Equal(end.Y - 30, cp2.Y);
        Assert.Equal(start.X, cp1.X);
        Assert.Equal(end.X, cp2.X);
    }

    [Fact]
    public void Route_with_mixed_horizontal_and_vertical_ports_pushes_independently()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 200, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));  // right edge → (+1, 0)
        var target = new Port(nodeB, new Point(50, 0));    // top edge   → (0, -1)

        var points = router.Route(source, target);
        var start = points[0];
        var cp1 = points[1];
        var cp2 = points[2];
        var end = points[3];

        // cp1 pushed horizontally only.
        Assert.Equal(start.Y, cp1.Y);
        Assert.True(cp1.X > start.X);

        // cp2 pushed vertically only.
        Assert.Equal(end.X, cp2.X);
        Assert.True(cp2.Y < end.Y);
    }

    [Fact]
    public void Route_classifies_corner_port_as_horizontal()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 100 };
        var nodeB = new Node { X = 300, Y = 300, Width = 100, Height = 100 };
        var source = new Port(nodeA, new Point(0, 0));  // top-left corner
        var target = new Port(nodeB, new Point(0, 0));  // top-left corner

        var points = router.Route(source, target);
        var start = points[0];
        var cp1 = points[1];
        var cp2 = points[2];
        var end = points[3];

        // Tie-break prefers horizontal → left emission → cp1.X < start.X, cp1.Y == start.Y.
        Assert.Equal(start.Y, cp1.Y);
        Assert.True(cp1.X < start.X);

        // Target is symmetric: same corner, same classification, same leftward push.
        Assert.Equal(end.Y, cp2.Y);
        Assert.True(cp2.X < end.X);
    }

    [Fact]
    public void Route_with_zero_size_owner_falls_back_to_horizontal()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0 };    // Width = Height = 0
        var nodeB = new Node { X = 200, Y = 0 };  // Width = Height = 0
        var source = new Port(nodeA, new Point(0, 0));
        var target = new Port(nodeB, new Point(0, 0));

        var points = router.Route(source, target);

        Assert.All(points, p =>
        {
            Assert.False(double.IsNaN(p.X));
            Assert.False(double.IsNaN(p.Y));
            Assert.False(double.IsInfinity(p.X));
            Assert.False(double.IsInfinity(p.Y));
        });

        // With all distances tied at 0, tie-break selects horizontal left emission.
        Assert.Equal(points[0].Y, points[1].Y);
        Assert.Equal(points[3].Y, points[2].Y);
    }

    [Fact]
    public void Route_with_port_outside_owner_emits_toward_the_side_it_sits_past()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
        // Source sits 10px to the left of nodeA's left edge — negative leftDist wins Min.
        var source = new Port(nodeA, new Point(-10, 25));
        var target = new Port(nodeB, new Point(0, 25));  // left edge

        var points = router.Route(source, target);
        var start = points[0];
        var cp1 = points[1];

        // Source should emit leftward (outward from the side it sits past).
        Assert.Equal(start.Y, cp1.Y);
        Assert.True(cp1.X < start.X,
            $"cp1.X ({cp1.X}) should be < start.X ({start.X}) for a port sitting left of its owner");
    }
}
