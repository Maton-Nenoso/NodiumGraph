using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class MinimapTests
{
    [Fact]
    public void GetMinimapBounds_BottomRight()
    {
        var bounds = MinimapRenderer.GetMinimapBounds(new Rect(0, 0, 800, 600), MinimapPosition.BottomRight);
        Assert.True(bounds.Right <= 800);
        Assert.True(bounds.Bottom <= 600);
        Assert.Equal(200, bounds.Width);
        Assert.Equal(150, bounds.Height);
    }

    [Fact]
    public void GetMinimapBounds_TopLeft()
    {
        var bounds = MinimapRenderer.GetMinimapBounds(new Rect(0, 0, 800, 600), MinimapPosition.TopLeft);
        Assert.Equal(10, bounds.X);
        Assert.Equal(10, bounds.Y);
    }

    [Fact]
    public void MinimapToWorld_returns_null_outside_minimap()
    {
        var graph = new Graph();
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 50;
        graph.AddNode(node);

        var result = MinimapRenderer.MinimapToWorld(
            new Point(0, 0), new Rect(0, 0, 800, 600), graph, MinimapPosition.BottomRight);

        Assert.Null(result);
    }

    [Fact]
    public void MinimapToWorld_returns_null_for_empty_graph()
    {
        var graph = new Graph();

        var result = MinimapRenderer.MinimapToWorld(
            new Point(700, 500), new Rect(0, 0, 800, 600), graph, MinimapPosition.BottomRight);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeWorldBounds_returns_correct_bounds()
    {
        var graph = new Graph();
        var a = new Node { X = 10, Y = 20 };
        a.Width = 50;
        a.Height = 30;
        var b = new Node { X = 100, Y = 80 };
        b.Width = 60;
        b.Height = 40;
        graph.AddNode(a);
        graph.AddNode(b);

        var bounds = MinimapRenderer.ComputeWorldBounds(graph);

        Assert.NotNull(bounds);
        Assert.Equal(10, bounds.Value.minX);
        Assert.Equal(20, bounds.Value.minY);
        Assert.Equal(160, bounds.Value.maxX);
        Assert.Equal(120, bounds.Value.maxY);
    }

    [Fact]
    public void ComputeWorldBounds_returns_null_for_empty_graph()
    {
        var graph = new Graph();
        Assert.Null(MinimapRenderer.ComputeWorldBounds(graph));
    }

    [AvaloniaFact]
    public void Canvas_with_minimap_enabled_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas { ShowMinimap = true };
        var graph = new Graph();
        var n = new Node { X = 100, Y = 100 };
        n.Width = 200;
        n.Height = 100;
        graph.AddNode(n);
        canvas.Graph = graph;
        // Just verify no exception in render path
    }
}
