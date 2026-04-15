using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        Assert.Empty(graph.SelectedItems);
        Assert.Empty(graph.SelectedConnections);
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

    [Fact]
    public void RemoveNode_resets_IsSelected()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        graph.Select(node);
        node.IsSelected = true; // accessible via InternalsVisibleTo

        graph.RemoveNode(node);

        Assert.False(node.IsSelected);
    }

    [Fact]
    public void Select_sets_IsSelected_true()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        graph.Select(node);

        Assert.True(node.IsSelected);
    }

    [Fact]
    public void Deselect_sets_IsSelected_false()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        graph.Select(node);

        graph.Deselect(node);

        Assert.False(node.IsSelected);
    }

    [Fact]
    public void ClearSelection_resets_all_IsSelected()
    {
        var graph = new Graph();
        var a = new Node();
        var b = new Node();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.Select(a);
        graph.Select(b);

        graph.ClearSelection();

        Assert.False(a.IsSelected);
        Assert.False(b.IsSelected);
    }

    [Fact]
    public void Select_same_node_twice_is_idempotent()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        graph.Select(node);
        graph.Select(node);

        Assert.Single(graph.SelectedNodes);
    }

    [Fact]
    public void RemoveNodes_batch_removes_all_with_connections()
    {
        var graph = new Graph();
        var a = new Node { X = 0, Y = 0 };
        var b = new Node { X = 100, Y = 0 };
        var c = new Node { X = 200, Y = 0 };
        a.PortProvider = new FixedPortProvider(new[] { new Port(a, new Point(50, 50)) });
        b.PortProvider = new FixedPortProvider(new[] { new Port(b, new Point(0, 50)) });
        c.PortProvider = new FixedPortProvider(new[] { new Port(c, new Point(0, 50)) });

        graph.AddNode(a);
        graph.AddNode(b);
        graph.AddNode(c);

        var conn1 = new Connection(a.PortProvider.Ports[0], b.PortProvider.Ports[0]);
        var conn2 = new Connection(a.PortProvider.Ports[0], c.PortProvider.Ports[0]);
        graph.AddConnection(conn1);
        graph.AddConnection(conn2);

        graph.RemoveNodes(new[] { a, b });

        Assert.Single(graph.Nodes);
        Assert.Same(c, graph.Nodes[0]);
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void RemoveNodes_batch_clears_IsSelected()
    {
        var graph = new Graph();
        var a = new Node();
        var b = new Node();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.Select(a);
        graph.Select(b);

        graph.RemoveNodes(new[] { a, b });

        Assert.False(a.IsSelected);
        Assert.False(b.IsSelected);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void SelectedItems_is_empty_after_construction()
    {
        var graph = new Graph();
        Assert.Empty(graph.SelectedItems);
        Assert.Empty(graph.SelectedNodes);
        Assert.Empty(graph.SelectedConnections);
    }

    [Fact]
    public void Adding_node_to_SelectedItems_appears_in_SelectedNodes_view()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        graph.SelectedItems.Add(node);

        Assert.Contains(node, graph.SelectedNodes);
        Assert.Empty(graph.SelectedConnections);
    }

    [Fact]
    public void Adding_connection_to_SelectedItems_appears_in_SelectedConnections_view()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var connection = new Connection(source, target);
        graph.AddConnection(connection);

        graph.SelectedItems.Add(connection);

        Assert.Contains(connection, graph.SelectedConnections);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void Mixed_selection_partitions_correctly()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var connection = new Connection(source, target);
        graph.AddConnection(connection);

        graph.SelectedItems.Add(node);
        graph.SelectedItems.Add(connection);

        Assert.Equal(2, graph.SelectedItems.Count);
        Assert.Single(graph.SelectedNodes);
        Assert.Single(graph.SelectedConnections);
    }

    [Fact]
    public void SelectedNodes_fires_CollectionChanged_on_node_add()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        NotifyCollectionChangedEventArgs? captured = null;
        ((INotifyCollectionChanged)graph.SelectedNodes).CollectionChanged += (_, e) => captured = e;

        graph.SelectedItems.Add(node);

        Assert.NotNull(captured);
        Assert.Equal(NotifyCollectionChangedAction.Add, captured!.Action);
        Assert.NotNull(captured.NewItems);
        Assert.Contains(node, captured.NewItems!.Cast<Node>());
    }

    [Fact]
    public void Removing_from_SelectedItems_removes_from_view()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var connection = new Connection(source, target);
        graph.AddConnection(connection);

        graph.SelectedItems.Add(node);
        graph.SelectedItems.Add(connection);

        graph.SelectedItems.Remove(node);
        graph.SelectedItems.Remove(connection);

        Assert.Empty(graph.SelectedNodes);
        Assert.Empty(graph.SelectedConnections);
        Assert.Empty(graph.SelectedItems);
    }

    [Fact]
    public void Clearing_SelectedItems_clears_views()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var connection = new Connection(source, target);
        graph.AddConnection(connection);

        graph.SelectedItems.Add(node);
        graph.SelectedItems.Add(connection);

        graph.SelectedItems.Clear();

        Assert.Empty(graph.SelectedNodes);
        Assert.Empty(graph.SelectedConnections);
        Assert.Empty(graph.SelectedItems);
    }

    [Fact]
    public void SelectedNodes_is_readonly_observable_collection()
    {
        var graph = new Graph();
        Assert.IsType<ReadOnlyObservableCollection<Node>>(graph.SelectedNodes);
        Assert.IsType<ReadOnlyObservableCollection<Connection>>(graph.SelectedConnections);
    }

    [Fact]
    public void SelectedItems_Add_rejects_foreign_node()
    {
        var g1 = new Graph();
        var g2 = new Graph();
        var foreign = new Node();
        g2.AddNode(foreign);

        Assert.Throws<InvalidOperationException>(() => g1.SelectedItems.Add(foreign));
    }

    [Fact]
    public void SelectedItems_Add_rejects_foreign_connection()
    {
        var g1 = new Graph();
        var g2 = new Graph();
        var node = new Node();
        g2.AddNode(node);
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var foreignConnection = new Connection(source, target);
        g2.AddConnection(foreignConnection);

        Assert.Throws<InvalidOperationException>(() => g1.SelectedItems.Add(foreignConnection));
    }

    [Fact]
    public void SelectedItems_Add_is_idempotent_on_duplicate()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        graph.SelectedItems.Add(node);
        graph.SelectedItems.Add(node);

        Assert.Single(graph.SelectedItems);
        Assert.Single(graph.SelectedNodes);
    }

    [Fact]
    public void SelectedItems_Add_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Graph().SelectedItems.Add(null!));
    }

    [Fact]
    public void Mutating_SelectedItems_from_view_handler_throws()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        ((INotifyCollectionChanged)graph.SelectedNodes).CollectionChanged += (_, _) =>
        {
            graph.SelectedItems.Clear(); // reentrant mutation
        };

        Assert.Throws<InvalidOperationException>(() => graph.SelectedItems.Add(node));
    }
}
