using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class StepRouterTests
{
    [Fact]
    public void Route_returns_orthogonal_segments()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[^1]);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var isHorizontal = Math.Abs(points[i].Y - points[i + 1].Y) < 0.001;
            var isVertical = Math.Abs(points[i].X - points[i + 1].X) < 0.001;
            Assert.True(isHorizontal || isVertical,
                $"Segment {i} is neither horizontal nor vertical: {points[i]} -> {points[i + 1]}");
        }
    }

    [Fact]
    public void Route_horizontal_aligned_returns_straight_horizontal()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        Assert.Equal(2, points.Count);
    }

    [Fact]
    public void Route_vertically_aligned_returns_two_points()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 100, Y = 50 };
        var nodeB = new Node { X = 100, Y = 200 };
        var source = new Port(nodeA, new Point(0, 0));  // AbsolutePosition = (100, 50)
        var target = new Port(nodeB, new Point(0, 0));  // AbsolutePosition = (100, 200)
        var points = router.Route(source, target);

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point(100, 50), points[0]);
        Assert.Equal(new Point(100, 200), points[1]);
    }

    [Fact]
    public void Route_both_ports_horizontal_returns_midX_HVH()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 100, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));  // right edge → H
        var target = new Port(nodeB, new Point(0, 25));    // left edge  → H

        var points = router.Route(source, target);

        Assert.Equal(4, points.Count);
        Assert.Equal(new Point(100, 25), points[0]);
        Assert.Equal(new Point(200, 25), points[1]);   // midX, start.Y
        Assert.Equal(new Point(200, 125), points[2]);  // midX, end.Y
        Assert.Equal(new Point(300, 125), points[3]);
    }

    [Fact]
    public void Route_both_ports_horizontal_aligned_row_returns_straight_line()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));  // right edge → H
        var target = new Port(nodeB, new Point(0, 25));    // left edge  → H

        var points = router.Route(source, target);

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point(100, 25), points[0]);
        Assert.Equal(new Point(300, 25), points[1]);
    }

    [Fact]
    public void Route_both_ports_vertical_returns_midY_VHV()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 200, Y = 300, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V (down)
        var target = new Port(nodeB, new Point(50, 0));     // top edge    → V (up)

        var points = router.Route(source, target);

        Assert.Equal(4, points.Count);
        Assert.Equal(new Point(50, 50), points[0]);
        Assert.Equal(new Point(50, 175), points[1]);    // start.X, midY
        Assert.Equal(new Point(250, 175), points[2]);   // end.X, midY
        Assert.Equal(new Point(250, 300), points[3]);
    }

    [Fact]
    public void Route_both_ports_vertical_aligned_column_returns_straight_line()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 0, Y = 300, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V
        var target = new Port(nodeB, new Point(50, 0));     // top edge    → V

        var points = router.Route(source, target);

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point(50, 50), points[0]);
        Assert.Equal(new Point(50, 300), points[1]);
    }

    [Fact]
    public void Route_source_horizontal_target_vertical_bends_at_end_X_start_Y()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 200, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(100, 25));   // right edge → H
        var target = new Port(nodeB, new Point(50, 0));     // top edge   → V (up)

        var points = router.Route(source, target);

        Assert.Equal(3, points.Count);
        Assert.Equal(new Point(100, 25), points[0]);
        Assert.Equal(new Point(350, 25), points[1]);   // end.X, start.Y
        Assert.Equal(new Point(350, 200), points[2]);
    }

    [Fact]
    public void Route_source_vertical_target_horizontal_bends_at_start_X_end_Y()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 200, Width = 100, Height = 50 };
        var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V (down)
        var target = new Port(nodeB, new Point(0, 25));     // left edge   → H

        var points = router.Route(source, target);

        Assert.Equal(3, points.Count);
        Assert.Equal(new Point(50, 50), points[0]);
        Assert.Equal(new Point(50, 225), points[1]);   // start.X, end.Y
        Assert.Equal(new Point(300, 225), points[2]);
    }
}
