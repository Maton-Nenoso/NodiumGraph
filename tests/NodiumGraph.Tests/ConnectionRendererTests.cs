using Avalonia;
using Avalonia.Media;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionRendererTests
{
    [Fact]
    public void CreateGeometry_returns_world_space_coordinates()
    {
        // Regression gate for the world-space refactor: CreateGeometry must emit
        // geometry in the same coordinate space as the routed points, regardless of
        // any viewport transform in effect. The caller (NodiumGraphCanvas) now owns
        // the viewport push so the cached geometry stays stable across pan/zoom.
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StraightRouter();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router);

        // Straight route goes from world (100,25) to world (200,125).
        // Bounds should match world-space, not any screen-space projection.
        var bounds = geometry.Bounds;
        Assert.Equal(100, bounds.X, 3);
        Assert.Equal(25, bounds.Y, 3);
        Assert.Equal(100, bounds.Width, 3);
        Assert.Equal(100, bounds.Height, 3);
    }

    [Fact]
    public void CreateGeometry_with_straight_router_returns_non_null()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StraightRouter();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router);

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

        var geometry = ConnectionRenderer.CreateGeometry(connection, router);

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

        var geometry = ConnectionRenderer.CreateGeometry(connection, router);

        Assert.NotNull(geometry);
    }

    [Fact]
    public void CreateGeometry_with_step_router_returns_stream_geometry_with_bounds()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StepRouter();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router);

        var streamGeo = Assert.IsType<StreamGeometry>(geometry);
        Assert.True(streamGeo.Bounds.Width > 0 || streamGeo.Bounds.Height > 0);
    }

    [Fact]
    public void CreateGeometry_with_bezier_router_returns_stream_geometry_with_bounds()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new BezierRouter();

        var geometry = ConnectionRenderer.CreateGeometry(connection, router);

        var streamGeo = Assert.IsType<StreamGeometry>(geometry);
        Assert.True(streamGeo.Bounds.Width > 0 || streamGeo.Bounds.Height > 0);
    }
}
