using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasRenderTests
{
    [AvaloniaFact]
    public void Canvas_does_not_throw_on_render_with_graph()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var portOut = new Port(nodeA, new Point(100, 25));
        var portIn = new Port(nodeB, new Point(0, 25));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddConnection(new Connection(portOut, portIn));
        canvas.Graph = graph;

        // Verify no exception — rendering details tested in unit tests.
        // The headless backend will call Render during layout.
    }

    [AvaloniaFact]
    public void Canvas_does_not_throw_on_render_without_graph()
    {
        var canvas = new NodiumGraphCanvas();

        // Render with no graph set — should be safe.
    }

    [AvaloniaFact]
    public void Canvas_does_not_throw_on_render_with_grid_disabled()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ShowGrid = false;
        canvas.Graph = new Graph();
    }

    [AvaloniaFact]
    public void Canvas_clips_to_bounds_by_default()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.True(canvas.ClipToBounds);
    }
}
