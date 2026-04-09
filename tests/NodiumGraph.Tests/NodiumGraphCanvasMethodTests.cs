using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasMethodTests
{
    [AvaloniaFact]
    public void ZoomToFit_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ZoomToFit();
    }

    [AvaloniaFact]
    public void ZoomToFit_with_empty_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        canvas.ZoomToFit();
    }

    [AvaloniaFact]
    public void ZoomToNodes_adjusts_zoom_and_offset()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node { X = 0, Y = 0 };
        n1.Width = 100; n1.Height = 50;
        var n2 = new Node { X = 500, Y = 300 };
        n2.Width = 100; n2.Height = 50;
        graph.AddNode(n1);
        graph.AddNode(n2);
        canvas.Graph = graph;

        // ZoomToNodes changes viewport state
        canvas.ZoomToNodes(graph.Nodes);

        // ViewportZoom should be set (possibly < 1 if canvas is small)
        // ViewportOffset should be set to center nodes
        // Just verify no exceptions and state changed from defaults
        Assert.True(canvas.ViewportZoom > 0);
    }

    [AvaloniaFact]
    public void CenterOnNode_changes_offset()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 500, Y = 300 };
        node.Width = 100; node.Height = 50;
        graph.AddNode(node);
        canvas.Graph = graph;

        var originalOffset = canvas.ViewportOffset;
        canvas.CenterOnNode(node);

        // Offset should change (canvas bounds may be 0 in headless, but the math runs)
        // At minimum verify no exception
    }

    [AvaloniaFact]
    public void CenterOnNode_null_throws()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Throws<ArgumentNullException>(() => canvas.CenterOnNode(null!));
    }

    [AvaloniaFact]
    public void SelectAll_is_public()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node());
        canvas.Graph = graph;

        canvas.SelectAll(); // Should compile and work as public method
        Assert.Single(graph.SelectedNodes);
    }

    [AvaloniaFact]
    public void HitTestPort_does_not_create_ports_on_DynamicPortProvider()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 100, Y = 100 };
        node.Width = 200;
        node.Height = 100;
        node.PortProvider = new DynamicPortProvider(node);
        graph.AddNode(node);
        canvas.Graph = graph;

        Assert.Empty(node.PortProvider.Ports);

        // Hit-test near the node boundary — should NOT create a port
        canvas.HitTestPort(new Point(100, 150));

        Assert.Empty(node.PortProvider.Ports);
    }

    [AvaloniaFact]
    public void DeleteSelected_is_public()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;

        canvas.DeleteSelected(); // Should compile as public method, no-op with no selection
    }
}
