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
