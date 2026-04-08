using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class GraphTests
{
    [Fact]
    public void New_graph_has_empty_collections()
    {
        var graph = new Graph();
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Connections);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void AddNode_adds_to_nodes_collection()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        Assert.Single(graph.Nodes);
        Assert.Contains(node, graph.Nodes);
    }

    [Fact]
    public void RemoveNode_removes_from_nodes_collection()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        graph.RemoveNode(node);
        Assert.Empty(graph.Nodes);
    }

    [Fact]
    public void RemoveNode_cascades_to_connected_connections()
    {
        var graph = new Graph();
        var nodeA = new Node();
        var nodeB = new Node();
        var portA = new Port(nodeA, new Point(0, 0));
        var portB = new Port(nodeB, new Point(0, 0));
        var connection = new Connection(portA, portB);

        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddConnection(connection);

        graph.RemoveNode(nodeA);

        Assert.Empty(graph.Connections);
        Assert.Single(graph.Nodes);
    }

    [Fact]
    public void AddConnection_adds_to_connections_collection()
    {
        var graph = new Graph();
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var conn = new Connection(source, target);
        graph.AddConnection(conn);
        Assert.Single(graph.Connections);
    }

    [Fact]
    public void RemoveConnection_removes_from_connections_collection()
    {
        var graph = new Graph();
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var conn = new Connection(source, target);
        graph.AddConnection(conn);
        graph.RemoveConnection(conn);
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void RemoveNode_removes_from_selection()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        graph.Select(node);
        Assert.Single(graph.SelectedNodes);

        graph.RemoveNode(node);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void AddNode_null_throws()
    {
        var graph = new Graph();
        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!));
    }

    [Fact]
    public void AddConnection_null_throws()
    {
        var graph = new Graph();
        Assert.Throws<ArgumentNullException>(() => graph.AddConnection(null!));
    }
}
