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
    public void DeleteSelected_reports_to_handler()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;
        canvas.SelectNode(node, false);

        IReadOnlyList<Node>? deletedNodes = null;
        IReadOnlyList<Connection>? deletedConns = null;
        canvas.NodeHandler = new TestNodeHandler((nodes, conns) =>
        {
            deletedNodes = nodes;
            deletedConns = conns;
        });

        canvas.DeleteSelected();

        Assert.NotNull(deletedNodes);
        Assert.Single(deletedNodes);
        Assert.Same(node, deletedNodes[0]);
        Assert.NotNull(deletedConns);
        Assert.Empty(deletedConns);
    }

    [AvaloniaFact]
    public void DeleteSelected_includes_affected_connections()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();

        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 0 };
        var portOut = new Port(nodeA, new Point(100, 25));
        var portIn = new Port(nodeB, new Point(0, 25));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        var conn = new Connection(portOut, portIn);
        graph.AddConnection(conn);
        canvas.Graph = graph;
        canvas.SelectNode(nodeA, false);

        IReadOnlyList<Connection>? reportedConns = null;
        canvas.NodeHandler = new TestNodeHandler((_, conns) => reportedConns = conns);

        canvas.DeleteSelected();

        Assert.NotNull(reportedConns);
        Assert.Single(reportedConns);
        Assert.Same(conn, reportedConns[0]);
    }

    [AvaloniaFact]
    public void DeleteSelected_does_not_mutate_graph()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;
        canvas.SelectNode(node, false);

        canvas.NodeHandler = new TestNodeHandler((_, _) => { });

        canvas.DeleteSelected();

        // Graph should still contain the node — handler decides what to do
        Assert.Contains(node, graph.Nodes);
        Assert.Single(graph.Nodes);
    }

    [AvaloniaFact]
    public void DeleteSelected_with_no_selection_does_not_call_handler()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node());
        canvas.Graph = graph;

        var called = false;
        canvas.NodeHandler = new TestNodeHandler((_, _) => called = true);

        canvas.DeleteSelected();

        Assert.False(called);
    }

    [AvaloniaFact]
    public void DeleteSelected_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.DeleteSelected();
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

    private class TestNodeHandler(Action<IReadOnlyList<Node>, IReadOnlyList<Connection>> onDelete)
        : INodeInteractionHandler
    {
        public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves) { }

        public void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections) =>
            onDelete(nodes, connections);

        public void OnNodeDoubleClicked(Node node) { }
    }

    private class TestSelectionHandler(Action<IReadOnlyList<Node>> callback) : ISelectionHandler
    {
        public void OnSelectionChanged(IReadOnlyList<Node> selectedNodes) => callback(selectedNodes);
    }
}
