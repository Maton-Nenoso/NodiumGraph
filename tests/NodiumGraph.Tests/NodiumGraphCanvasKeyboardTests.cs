using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasKeyboardTests
{
    [AvaloniaFact]
    public void SelectAll_selects_all_nodes()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node();
        var n2 = new Node();
        var n3 = new Node();
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);
        canvas.Graph = graph;

        canvas.SelectAll();

        Assert.Equal(3, graph.SelectedNodes.Count);
        Assert.True(n1.IsSelected);
        Assert.True(n2.IsSelected);
        Assert.True(n3.IsSelected);
    }

    [AvaloniaFact]
    public void SelectAll_notifies_selection_handler()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node();
        var n2 = new Node();
        graph.AddNode(n1);
        graph.AddNode(n2);
        canvas.Graph = graph;

        IReadOnlyList<Node>? notified = null;
        canvas.SelectionHandler = new TestSelectionHandler(nodes => notified = nodes);

        canvas.SelectAll();

        Assert.NotNull(notified);
        Assert.Equal(2, notified.Count);
    }

    [AvaloniaFact]
    public void SelectAll_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.SelectAll();
        // No exception = pass
    }

    [AvaloniaFact]
    public void Escape_clears_selection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        canvas.Graph = graph;
        canvas.SelectNode(node, false);

        Assert.True(node.IsSelected);

        canvas.ClearSelection();

        Assert.False(node.IsSelected);
        Assert.Empty(graph.SelectedNodes);
    }

}
