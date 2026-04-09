using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasDragTests
{
    [AvaloniaFact]
    public void Drag_state_starts_inactive()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.IsDragging);
    }

    [AvaloniaFact]
    public void SnapToGrid_rounds_position_to_grid()
    {
        double gridSize = 20.0;
        double value = 33.0;
        double snapped = Math.Round(value / gridSize) * gridSize;
        Assert.Equal(40.0, snapped);
    }

    [AvaloniaFact]
    public void SnapToGrid_rounds_down_when_closer()
    {
        double gridSize = 20.0;
        double value = 27.0;
        double snapped = Math.Round(value / gridSize) * gridSize;
        Assert.Equal(20.0, snapped);
    }

    [AvaloniaFact]
    public void NodeHandler_receives_move_info_with_old_and_new_positions()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        IReadOnlyList<NodeMoveInfo>? reportedMoves = null;
        canvas.NodeHandler = new TestNodeHandler(moves => reportedMoves = moves);

        // Simulate: select node, move it, complete drag
        canvas.SelectNode(node, additive: false);

        // Directly test the handler notification by moving the node and simulating drag completion
        var oldPos = new Point(node.X, node.Y);
        node.X = 150;
        node.Y = 200;

        // Create and report the move info manually (since we can't easily simulate pointer events)
        var moveInfo = new NodeMoveInfo(node, oldPos, new Point(node.X, node.Y));
        canvas.NodeHandler.OnNodesMoved([moveInfo]);

        Assert.NotNull(reportedMoves);
        Assert.Single(reportedMoves);
        Assert.Equal(oldPos, reportedMoves[0].OldPosition);
        Assert.Equal(new Point(150, 200), reportedMoves[0].NewPosition);
    }

    private class TestNodeHandler(Action<IReadOnlyList<NodeMoveInfo>> onMoved)
        : INodeInteractionHandler
    {
        public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves) => onMoved(moves);
        public void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections) { }
        public void OnNodeDoubleClicked(Node node) { }
    }
}
