using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasCuttingTests
{
    [AvaloniaFact]
    public void Cutting_state_starts_inactive()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.IsCuttingConnections);
    }

    [Fact]
    public void LinesIntersect_detects_crossing_segments()
    {
        Assert.True(NodiumGraphCanvas.LinesIntersect(
            new Point(0, 0), new Point(10, 10),
            new Point(0, 10), new Point(10, 0)));
    }

    [Fact]
    public void LinesIntersect_detects_non_crossing_segments()
    {
        Assert.False(NodiumGraphCanvas.LinesIntersect(
            new Point(0, 0), new Point(10, 0),
            new Point(0, 5), new Point(10, 5)));
    }

    [Fact]
    public void LinesIntersect_non_overlapping_segments_do_not_intersect()
    {
        // Two segments that would intersect if extended, but don't actually overlap
        Assert.False(NodiumGraphCanvas.LinesIntersect(
            new Point(0, 0), new Point(1, 1),
            new Point(5, 0), new Point(6, 1)));
    }

    [Fact]
    public void LinesIntersect_detects_collinear_overlap()
    {
        Assert.True(NodiumGraphCanvas.LinesIntersect(
            new Point(0, 0), new Point(10, 0),
            new Point(5, 0), new Point(15, 0)));
    }

    [Fact]
    public void CuttingLineIntersects_step_route_uses_polyline_not_bezier()
    {
        // StepRouter returns 4 points but they're orthogonal segments, not bezier
        var canvas = new NodiumGraphCanvas();
        canvas.ConnectionRouter = new StepRouter();
        var graph = new Graph();

        var nodeA = new Node { X = 0, Y = 0 };
        nodeA.Width = 100; nodeA.Height = 60;
        var nodeB = new Node { X = 200, Y = 100 };
        nodeB.Width = 100; nodeB.Height = 60;
        var portOut = new Port(nodeA, new Point(100, 30));
        var portIn = new Port(nodeB, new Point(0, 30));
        nodeA.PortProvider = new FixedPortProvider(new[] { portOut });
        nodeB.PortProvider = new FixedPortProvider(new[] { portIn });
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddConnection(new Connection(portOut, portIn));
        canvas.Graph = graph;

        // The step route goes: (100,30) -> (150,30) -> (150,130) -> (200,130)
        // A horizontal cutting line through Y=80 should intersect the vertical segment
        var transform = new ViewportTransform(1.0, default);
        var intersects = canvas.CuttingLineIntersectsGeometry(
            new Point(140, 80), new Point(160, 80),
            graph.Connections.First(), transform);

        Assert.True(intersects);
    }

    [Fact]
    public void BezierPoint_at_t0_returns_start()
    {
        var result = NodiumGraphCanvas.BezierPoint(
            new Point(0, 0), new Point(33, 0), new Point(66, 0), new Point(100, 0), 0.0);
        Assert.Equal(0, result.X, 3);
        Assert.Equal(0, result.Y, 3);
    }

    [Fact]
    public void BezierPoint_at_t1_returns_end()
    {
        var result = NodiumGraphCanvas.BezierPoint(
            new Point(0, 0), new Point(33, 0), new Point(66, 0), new Point(100, 0), 1.0);
        Assert.Equal(100, result.X, 3);
        Assert.Equal(0, result.Y, 3);
    }
}
