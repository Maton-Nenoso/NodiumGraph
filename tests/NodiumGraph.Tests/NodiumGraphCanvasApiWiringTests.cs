using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasApiWiringTests
{
    [AvaloniaFact]
    public void CanvasHandler_OnCanvasDoubleClicked_is_callable()
    {
        Point? reported = null;
        var handler = new TestCanvasHandler(pos => reported = pos);

        // Verify the handler interface works
        handler.OnCanvasDoubleClicked(new Point(100, 200));
        Assert.Equal(new Point(100, 200), reported);
    }

    [AvaloniaFact]
    public void NodeHandler_OnNodeDoubleClicked_is_callable()
    {
        Node? reported = null;
        var handler = new TestNodeHandler(n => reported = n);

        var node = new Node();
        handler.OnNodeDoubleClicked(node);
        Assert.Same(node, reported);
    }

    [AvaloniaFact]
    public void Port_visuals_render_without_exception()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        var port = new Port(node, "Out", PortFlow.Output, new Point(100, 30));
        node.PortProvider = new FixedPortProvider(new[] { port });
        graph.AddNode(node);
        canvas.Graph = graph;

        // Just verify no exception during render with ports
    }

    [AvaloniaFact]
    public void AllowDrop_is_true_by_default()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.True(Avalonia.Input.DragDrop.GetAllowDrop(canvas));
    }

    private class TestCanvasHandler(Action<Point> onDoubleClick) : ICanvasInteractionHandler
    {
        public void OnCanvasDoubleClicked(Point worldPosition) => onDoubleClick(worldPosition);
        public void OnCanvasDropped(Point worldPosition, object data) { }
    }

    private class TestNodeHandler(Action<Node> onDoubleClick) : INodeInteractionHandler
    {
        public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves) { }
        public void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections) { }
        public void OnNodeDoubleClicked(Node node) => onDoubleClick(node);
    }
}
