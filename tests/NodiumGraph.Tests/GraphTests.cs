using NodiumGraph.Model;
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
        graph.AddNode(node);
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
        graph.AddNode(node);
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

    [Fact]
    public void Select_node_not_in_graph_throws()
    {
        var graph = new Graph();
        var node = new Node();
        Assert.Throws<InvalidOperationException>(() => graph.Select(node));
    }

    [Fact]
    public void AddNode_duplicate_throws()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        Assert.Throws<InvalidOperationException>(() => graph.AddNode(node));
    }

    [Fact]
    public void AddConnection_duplicate_throws()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        var source = new Port(node, new Avalonia.Point(0, 0));
        var target = new Port(node, new Avalonia.Point(10, 0));
        var conn = new Connection(source, target);
        graph.AddConnection(conn);
        Assert.Throws<InvalidOperationException>(() => graph.AddConnection(conn));
    }

    [Fact]
    public void Deselect_removes_from_selection()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        graph.Select(node);
        graph.Deselect(node);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void ClearSelection_removes_all()
    {
        var graph = new Graph();
        var node1 = new Node();
        var node2 = new Node();
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.Select(node1);
        graph.Select(node2);
        Assert.Equal(2, graph.SelectedNodes.Count);

        graph.ClearSelection();
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void RemoveConnection_is_idempotent()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var conn = new Connection(source, target);
        graph.AddConnection(conn);
        graph.RemoveConnection(conn);
        // Second remove should not throw
        graph.RemoveConnection(conn);
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void Deselect_is_idempotent()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        // Deselect without prior select should not throw
        graph.Deselect(node);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void Nodes_collection_is_read_only()
    {
        var graph = new Graph();
        Assert.IsType<System.Collections.ObjectModel.ReadOnlyObservableCollection<Node>>(graph.Nodes);
    }

    [Fact]
    public void Connections_collection_is_read_only()
    {
        var graph = new Graph();
        Assert.IsType<System.Collections.ObjectModel.ReadOnlyObservableCollection<Connection>>(graph.Connections);
    }

    [Fact]
    public void AddConnection_throws_when_source_owner_not_in_graph()
    {
        var graph = new Graph();
        var nodeA = new Node();
        var nodeB = new Node();
        graph.AddNode(nodeB); // only B in graph
        var portA = new Port(nodeA, new Point(0, 0));
        var portB = new Port(nodeB, new Point(0, 0));

        Assert.Throws<InvalidOperationException>(() =>
            graph.AddConnection(new Connection(portA, portB)));
    }

    [Fact]
    public void AddConnection_throws_when_target_owner_not_in_graph()
    {
        var graph = new Graph();
        var nodeA = new Node();
        var nodeB = new Node();
        graph.AddNode(nodeA); // only A in graph
        var portA = new Port(nodeA, new Point(0, 0));
        var portB = new Port(nodeB, new Point(0, 0));

        Assert.Throws<InvalidOperationException>(() =>
            graph.AddConnection(new Connection(portA, portB)));
    }
}
