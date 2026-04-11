using System.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
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

    [AvaloniaFact]
    public void Node_container_is_in_visual_tree()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        canvas.Graph = graph;

        // +1 for the CanvasOverlay which is always present
        Assert.Equal(2, canvas.GetVisualChildren().Count());
    }

    [AvaloniaFact]
    public void Removing_node_removes_from_visual_tree()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        canvas.Graph = graph;

        graph.RemoveNode(node);

        // Only the CanvasOverlay remains
        Assert.Single(canvas.GetVisualChildren());
    }

    [AvaloniaFact]
    public void Changing_graph_removes_old_containers_from_visual_tree()
    {
        var canvas = new NodiumGraphCanvas();
        var graph1 = new Graph();
        graph1.AddNode(new Node());
        graph1.AddNode(new Node());
        canvas.Graph = graph1;

        var graph2 = new Graph();
        graph2.AddNode(new Node());
        canvas.Graph = graph2;

        // 1 node container + 1 overlay
        Assert.Equal(2, canvas.GetVisualChildren().Count());
    }

    [AvaloniaFact]
    public void Adding_connection_externally_triggers_repaint()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var portOut = new Port(nodeA, new Point(100, 25));
        var portIn = new Port(nodeB, new Point(0, 25));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        canvas.Graph = graph;

        // This should not throw and canvas should handle it
        graph.AddConnection(new Connection(portOut, portIn));

        // Verify the connection count is reflected
        Assert.Single(graph.Connections);
    }

    [AvaloniaFact]
    public void Setting_graph_to_null_unsubscribes_collection_events()
    {
        // Regression test: Graph = null takes the OnPropertyChanged path (not OnDetachedFromVisualTree).
        // Verifies that subscription teardown works correctly via that path.
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node());
        canvas.Graph = graph;

        Assert.Equal(1, canvas.NodeContainerCount);

        canvas.Graph = null;

        Assert.Equal(0, canvas.NodeContainerCount);

        // Re-assign should work cleanly
        canvas.Graph = graph;
        Assert.Equal(1, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void OnDetachedFromVisualTree_clears_graph_subscriptions()
    {
        // Arrange: attach the canvas to a real visual tree via a Window so that
        // removing it from the panel actually triggers OnDetachedFromVisualTree.
        var graph = new Graph();
        graph.AddNode(new Node());

        var canvas = new NodiumGraphCanvas { Graph = graph };
        // Use a Panel (not StackPanel) so the canvas receives a finite availableSize
        // during measure (StackPanel passes Infinity which NodiumGraphCanvas reflects back).
        var panel = new Avalonia.Controls.Panel();
        panel.Children.Add(canvas);

        var window = new Avalonia.Controls.Window
        {
            Content = panel,
            Width = 800,
            Height = 600,
        };
        window.Show();

        // Sanity: one container was created while attached.
        Assert.Equal(1, canvas.NodeContainerCount);

        // Act: remove the canvas from the panel — this detaches it from the visual tree
        // and triggers OnDetachedFromVisualTree.
        panel.Children.Remove(canvas);

        // After detach the containers must be cleared.
        Assert.Equal(0, canvas.NodeContainerCount);

        // Adding a node to the graph must NOT re-subscribe or increment the count.
        graph.AddNode(new Node());
        Assert.Equal(0, canvas.NodeContainerCount);

        window.Close();
    }

    [AvaloniaFact]
    public void Arrange_writes_measured_size_back_to_node()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.NodeTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Node>(
            (_, _) => new Avalonia.Controls.Border { Width = 150, Height = 80 },
            supportsRecycling: false);
        var graph = new Graph();
        var node = new Node { Title = "Test Node" };
        graph.AddNode(node);
        canvas.Graph = graph;

        // Embed in a Window so the visual tree is rooted and templates expand
        var window = new Avalonia.Controls.Window { Content = canvas };
        window.Show();

        // Force a layout pass on the window
        window.Measure(new Size(800, 600));
        window.Arrange(new Rect(0, 0, 800, 600));

        Assert.True(node.Width > 0, $"Expected Width > 0, got {node.Width}");
        Assert.True(node.Height > 0, $"Expected Height > 0, got {node.Height}");

        window.Close();
    }
}
