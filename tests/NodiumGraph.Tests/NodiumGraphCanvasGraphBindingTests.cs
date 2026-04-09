using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasGraphBindingTests
{
    [AvaloniaFact]
    public void Setting_graph_with_existing_nodes_creates_containers()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node());
        graph.AddNode(new Node());

        canvas.Graph = graph;

        Assert.Equal(2, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Adding_node_to_graph_creates_container()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;

        graph.AddNode(new Node());

        Assert.Equal(1, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Removing_node_from_graph_removes_container()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        canvas.Graph = graph;

        graph.RemoveNode(node);

        Assert.Equal(0, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Changing_graph_clears_old_and_loads_new()
    {
        var canvas = new NodiumGraphCanvas();
        var graph1 = new Graph();
        graph1.AddNode(new Node());
        graph1.AddNode(new Node());
        canvas.Graph = graph1;

        var graph2 = new Graph();
        graph2.AddNode(new Node());
        canvas.Graph = graph2;

        Assert.Equal(1, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Setting_graph_to_null_clears_containers()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node());
        canvas.Graph = graph;

        canvas.Graph = null;

        Assert.Equal(0, canvas.NodeContainerCount);
    }
}
