using System.Collections.Generic;
using System.Linq;
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

    [AvaloniaFact]
    public void Delete_key_fires_GraphInteractionHandler_with_snapshot()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node();
        var nodeB = new Node();
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        graph.AddConnection(connection);
        canvas.Graph = graph;

        graph.SelectedItems.Add(nodeA);
        graph.SelectedItems.Add(connection);

        var spy = new GraphInteractionHandlerSpy();
        canvas.GraphInteractionHandler = spy;

        var handled = canvas.TryHandleDeleteKey();

        Assert.True(handled);
        Assert.Single(spy.Calls);
        Assert.Equal(2, spy.Calls[0].Count);
        Assert.Contains(nodeA, spy.Calls[0]);
        Assert.Contains(connection, spy.Calls[0]);
    }

    [AvaloniaFact]
    public void Delete_key_with_empty_selection_does_not_fire()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;

        var spy = new GraphInteractionHandlerSpy();
        canvas.GraphInteractionHandler = spy;

        var handled = canvas.TryHandleDeleteKey();

        Assert.False(handled);
        Assert.Empty(spy.Calls);
    }

    [AvaloniaFact]
    public void Delete_key_with_null_handler_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        canvas.Graph = graph;
        graph.SelectedItems.Add(node);

        canvas.GraphInteractionHandler = null;

        var handled = canvas.TryHandleDeleteKey();

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Delete_key_passes_snapshot_not_live_collection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node();
        var nodeB = new Node();
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        canvas.Graph = graph;
        graph.SelectedItems.Add(nodeA);
        graph.SelectedItems.Add(nodeB);

        var spy = new GraphInteractionHandlerSpy();
        canvas.GraphInteractionHandler = spy;

        canvas.TryHandleDeleteKey();

        // Mutate the live collection after the handler fired — the snapshot must be unaffected.
        graph.SelectedItems.Clear();

        Assert.Single(spy.Calls);
        Assert.Equal(2, spy.Calls[0].Count);
        Assert.Contains(nodeA, spy.Calls[0]);
        Assert.Contains(nodeB, spy.Calls[0]);
    }

    private sealed class GraphInteractionHandlerSpy : IGraphInteractionHandler
    {
        public List<IReadOnlyCollection<IGraphElement>> Calls { get; } = new();

        public void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)
        {
            Calls.Add(elements.ToList());
        }
    }
}
