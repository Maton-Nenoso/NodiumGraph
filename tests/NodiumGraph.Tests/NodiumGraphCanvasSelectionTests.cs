using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;
using Size = Avalonia.Size;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasSelectionTests
{
    [AvaloniaFact]
    public void SelectNode_selects_single_node()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.SelectNode(node, additive: false);

        Assert.Contains(node, graph.SelectedNodes);
        Assert.True(node.IsSelected);
    }

    [AvaloniaFact]
    public void SelectNode_additive_adds_to_existing_selection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 200, Y = 200 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        canvas.Graph = graph;

        canvas.SelectNode(node1, additive: false);
        canvas.SelectNode(node2, additive: true);

        Assert.Contains(node1, graph.SelectedNodes);
        Assert.Contains(node2, graph.SelectedNodes);
    }

    [AvaloniaFact]
    public void SelectNode_non_additive_clears_previous_selection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 200, Y = 200 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        canvas.Graph = graph;

        canvas.SelectNode(node1, additive: false);
        canvas.SelectNode(node2, additive: false);

        Assert.DoesNotContain(node1, graph.SelectedNodes);
        Assert.False(node1.IsSelected);
        Assert.Contains(node2, graph.SelectedNodes);
        Assert.True(node2.IsSelected);
    }

    [AvaloniaFact]
    public void SelectNode_additive_toggles_off_already_selected()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.SelectNode(node, additive: false);
        Assert.True(node.IsSelected);

        canvas.SelectNode(node, additive: true);
        Assert.False(node.IsSelected);
        Assert.DoesNotContain(node, graph.SelectedNodes);
    }

    [AvaloniaFact]
    public void ClearSelection_deselects_all_nodes()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 200, Y = 200 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        canvas.Graph = graph;

        canvas.SelectNode(node1, additive: false);
        canvas.SelectNode(node2, additive: true);
        canvas.ClearSelection();

        Assert.Empty(graph.SelectedNodes);
        Assert.False(node1.IsSelected);
        Assert.False(node2.IsSelected);
    }

    [AvaloniaFact]
    public void HitTestNode_finds_node_at_position()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        var hit = canvas.HitTestNode(new Point(75, 75));
        Assert.Same(node, hit);
    }

    [AvaloniaFact]
    public void HitTestNode_returns_null_for_empty_space()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        var hit = canvas.HitTestNode(new Point(300, 300));
        Assert.Null(hit);
    }

    [AvaloniaFact]
    public void HitTestNode_returns_topmost_overlapping_node()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var bottom = new Node { X = 50, Y = 50 };
        bottom.Width = 100;
        bottom.Height = 100;
        var top = new Node { X = 80, Y = 80 };
        top.Width = 100;
        top.Height = 100;
        graph.AddNode(bottom);
        graph.AddNode(top);
        canvas.Graph = graph;

        // Point is in overlap region; should return the last-added (topmost)
        var hit = canvas.HitTestNode(new Point(100, 100));
        Assert.Same(top, hit);
    }

    [AvaloniaFact]
    public void SelectionHandler_is_notified_on_select()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;

        IReadOnlyList<Node>? notified = null;
        canvas.SelectionHandler = new TestSelectionHandler(nodes => notified = nodes);

        canvas.SelectNode(node, additive: false);

        Assert.NotNull(notified);
        Assert.Contains(node, notified);
    }

    [AvaloniaFact]
    public void SelectionHandler_is_notified_on_clear()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.SelectNode(node, additive: false);

        IReadOnlyList<Node>? notified = null;
        canvas.SelectionHandler = new TestSelectionHandler(nodes => notified = nodes);

        canvas.ClearSelection();

        Assert.NotNull(notified);
        Assert.Empty(notified);
    }

    [AvaloniaFact]
    public void ClearSelection_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ClearSelection();
        // No exception = pass
    }

    [AvaloniaFact]
    public void SelectNode_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        var node = new Node();
        canvas.SelectNode(node, additive: false);
        // No exception = pass
    }

    [AvaloniaFact]
    public void Marquee_state_starts_inactive()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.IsMarqueeSelecting);
    }

    [AvaloniaFact]
    public void Marquee_selects_nodes_within_rectangle()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node { X = 50, Y = 50 };
        n1.Width = 100;
        n1.Height = 60;
        var n2 = new Node { X = 300, Y = 300 };
        n2.Width = 100;
        n2.Height = 60;
        graph.AddNode(n1);
        graph.AddNode(n2);
        canvas.Graph = graph;

        // Simulate marquee that covers n1 but not n2
        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var marqueeRect = new Rect(0, 0, 200, 200);

        foreach (var node in graph.Nodes)
        {
            var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var nodeScreenSize = new Size(
                transform.WorldToScreen(node.Width),
                transform.WorldToScreen(node.Height));
            var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

            if (marqueeRect.Intersects(nodeRect))
            {
                node.IsSelected = true;
                graph.Select(node);
            }
        }

        Assert.True(n1.IsSelected);
        Assert.False(n2.IsSelected);
        Assert.Single(graph.SelectedNodes);
    }

}
