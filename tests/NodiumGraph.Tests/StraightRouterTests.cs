using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class StraightRouterTests
{
    [Fact]
    public void Route_returns_two_points_source_and_target()
    {
        var router = new StraightRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 50));
        var target = new Port(nodeB, new Point(0, 50));

        var points = router.Route(source, target);

        Assert.Equal(2, points.Count);
        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[1]);
    }
}
