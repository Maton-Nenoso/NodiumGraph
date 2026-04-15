using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class ISelectionHandlerTests
{
    private sealed class SelectionSpy : ISelectionHandler
    {
        public List<IReadOnlyCollection<IGraphElement>> Calls { get; } = new();

        public void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected)
            => Calls.Add(selected.ToList());
    }

    [Fact]
    public void OnSelectionChanged_receives_mixed_elements()
    {
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 100, Y = 0 };
        graph.AddNode(node1);
        graph.AddNode(node2);

        var port1 = new Port(node1, new Point(0, 0));
        var port2 = new Port(node2, new Point(0, 0));
        var connection = new Connection(port1, port2);
        graph.AddConnection(connection);

        graph.SelectedItems.Add(node1);
        graph.SelectedItems.Add(connection);

        var spy = new SelectionSpy();
        spy.OnSelectionChanged(graph.SelectedItems);

        Assert.Single(spy.Calls);
        var payload = spy.Calls[0];
        Assert.Equal(2, payload.Count);
        Assert.Contains(node1, payload);
        Assert.Contains(connection, payload);
        Assert.Single(payload.OfType<Node>());
        Assert.Single(payload.OfType<Connection>());
    }

    [Fact]
    public void Handler_receives_snapshot_not_live_collection()
    {
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 100, Y = 0 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.SelectedItems.Add(node1);

        var spy = new SelectionSpy();
        // Simulate what the canvas does: snapshot via ToArray before firing.
        spy.OnSelectionChanged(graph.SelectedItems.ToArray());

        // Now mutate the graph's selection.
        graph.SelectedItems.Add(node2);

        // Received collection should be unchanged.
        var received = spy.Calls[0];
        Assert.Single(received);
        Assert.Same(node1, received.Single());
    }

    [Fact]
    public void OnSelectionChanged_receives_empty_collection_on_clear()
    {
        var graph = new Graph();
        var node = new Node { X = 0, Y = 0 };
        graph.AddNode(node);
        graph.SelectedItems.Add(node);

        graph.SelectedItems.Clear();

        var spy = new SelectionSpy();
        spy.OnSelectionChanged(graph.SelectedItems);

        Assert.Single(spy.Calls);
        Assert.Empty(spy.Calls[0]);
    }
}
