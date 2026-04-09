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
}
