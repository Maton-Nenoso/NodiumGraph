using Avalonia;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionRendererTests
{
    [Fact]
    public void CreateGeometry_with_straight_router_returns_non_null()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StraightRouter();
        var transform = new ViewportTransform(1.0, default);

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, transform);

        Assert.NotNull(geometry);
    }

    [Fact]
    public void CreateGeometry_with_bezier_router_returns_non_null()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new BezierRouter();
        var transform = new ViewportTransform(1.0, default);

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, transform);

        Assert.NotNull(geometry);
    }

    [Fact]
    public void CreateGeometry_with_step_router_returns_non_null()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StepRouter();
        var transform = new ViewportTransform(1.0, default);

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, transform);

        Assert.NotNull(geometry);
    }
}
