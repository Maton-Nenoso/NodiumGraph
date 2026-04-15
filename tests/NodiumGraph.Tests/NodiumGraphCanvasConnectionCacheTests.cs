using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

/// <summary>
/// Pins the per-connection world-space geometry cache on <see cref="NodiumGraphCanvas"/>.
/// The cache feeds Task 13 hit-testing: cache hits must skip
/// <c>ConnectionRenderer.CreateRenderable</c> so click handlers can reuse the
/// same <see cref="ConnectionRenderable"/> that was painted.
/// </summary>
public class NodiumGraphCanvasConnectionCacheTests
{
    private const int CanvasWidth = 800;
    private const int CanvasHeight = 600;

    private static (NodiumGraphCanvas canvas, Graph graph, Connection connection) BuildCanvasWithConnection(
        Point sourcePos, Point targetPos)
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node { X = sourcePos.X, Y = sourcePos.Y };
        var nodeB = new Node { X = targetPos.X, Y = targetPos.Y };
        var portOut = new Port(nodeA, new Point(0, 0));
        var portIn = new Port(nodeB, new Point(0, 0));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        var connection = new Connection(portOut, portIn);
        graph.AddConnection(connection);
        canvas.Graph = graph;

        canvas.Measure(new Size(CanvasWidth, CanvasHeight));
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));
        return (canvas, graph, connection);
    }

    private static void DriveRender(NodiumGraphCanvas canvas)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(CanvasWidth, CanvasHeight));
        using var ctx = bitmap.CreateDrawingContext();
        canvas.Render(ctx);
    }

    [AvaloniaFact]
    public void Connection_cache_populated_after_first_render()
    {
        var (canvas, _, connection) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        DriveRender(canvas);

        Assert.True(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Cache must contain the connection after a render pass placed it inside the viewport.");
    }

    [AvaloniaFact]
    public void Connection_cache_survives_pan()
    {
        var (canvas, _, connection) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        DriveRender(canvas);
        var firstStroke = canvas.TryGetCachedConnectionStroke(connection.Id);
        Assert.NotNull(firstStroke);

        // Pan: connection should still be inside the viewport.
        canvas.ViewportOffset = new Point(-20, -10);
        DriveRender(canvas);

        var secondStroke = canvas.TryGetCachedConnectionStroke(connection.Id);
        Assert.NotNull(secondStroke);
        Assert.Same(firstStroke, secondStroke);
    }

    [AvaloniaFact]
    public void Off_screen_connection_not_cached()
    {
        // Connection sits far outside the 800x600 viewport at (1,1) zoom.
        var (canvas, _, connection) = BuildCanvasWithConnection(
            new Point(5000, 5000), new Point(5200, 5100));

        DriveRender(canvas);

        Assert.False(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Off-screen connection must not be inserted into the cache.");
    }

    [AvaloniaFact]
    public void RemoveConnection_invalidates_cache_entry()
    {
        var (canvas, graph, connection) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        graph.RemoveConnection(connection);

        Assert.False(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Removing a connection must drop its cached geometry.");
    }

    [AvaloniaFact]
    public void MoveNode_invalidates_touching_connections()
    {
        // Three nodes, two connections, both touching n1.
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node { X = 100, Y = 100 };
        var n2 = new Node { X = 300, Y = 100 };
        var n3 = new Node { X = 300, Y = 250 };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);
        var c12 = new Connection(new Port(n1, new Point(0, 0)), new Port(n2, new Point(0, 0)));
        var c13 = new Connection(new Port(n1, new Point(0, 0)), new Port(n3, new Point(0, 0)));
        graph.AddConnection(c12);
        graph.AddConnection(c13);
        canvas.Graph = graph;
        canvas.Measure(new Size(CanvasWidth, CanvasHeight));
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));

        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(c12.Id));
        Assert.True(canvas.ConnectionGeometryCacheContains(c13.Id));

        n1.X += 25;

        Assert.False(
            canvas.ConnectionGeometryCacheContains(c12.Id),
            "Connection touching moved node must drop cached geometry.");
        Assert.False(
            canvas.ConnectionGeometryCacheContains(c13.Id),
            "Connection touching moved node must drop cached geometry.");

        // Subsequent render repopulates.
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));
        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(c12.Id));
        Assert.True(canvas.ConnectionGeometryCacheContains(c13.Id));
    }

    [AvaloniaFact]
    public void UnrelatedNode_move_does_not_affect_cache()
    {
        // Two disjoint connection pairs: n1->n2 and n3->n4.
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node { X = 100, Y = 100 };
        var n2 = new Node { X = 250, Y = 100 };
        var n3 = new Node { X = 100, Y = 250 };
        var n4 = new Node { X = 250, Y = 250 };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);
        graph.AddNode(n4);
        var c12 = new Connection(new Port(n1, new Point(0, 0)), new Port(n2, new Point(0, 0)));
        var c34 = new Connection(new Port(n3, new Point(0, 0)), new Port(n4, new Point(0, 0)));
        graph.AddConnection(c12);
        graph.AddConnection(c34);
        canvas.Graph = graph;
        canvas.Measure(new Size(CanvasWidth, CanvasHeight));
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));

        DriveRender(canvas);
        var c12Stroke = canvas.TryGetCachedConnectionStroke(c12.Id);
        Assert.NotNull(c12Stroke);
        Assert.True(canvas.ConnectionGeometryCacheContains(c34.Id));

        // Move n3: only c34 should be invalidated; c12 should keep its cached entry.
        n3.X += 30;

        Assert.False(
            canvas.ConnectionGeometryCacheContains(c34.Id),
            "Connection touching moved node must be invalidated.");
        var c12StrokeAfter = canvas.TryGetCachedConnectionStroke(c12.Id);
        Assert.NotNull(c12StrokeAfter);
        Assert.Same(c12Stroke, c12StrokeAfter);
    }

    [AvaloniaFact]
    public void SwapRouter_clears_entire_cache()
    {
        var (canvas, _, connection) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        canvas.ConnectionRouter = new StepRouter();

        Assert.Equal(0, canvas.ConnectionGeometryCacheCount);
    }

    [AvaloniaFact]
    public void SwapDefaultConnectionStyle_clears_entire_cache()
    {
        var (canvas, _, connection) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        canvas.DefaultConnectionStyle = new ConnectionStyle();

        Assert.Equal(0, canvas.ConnectionGeometryCacheCount);
    }

    [AvaloniaFact]
    public void ThemeChange_clears_cache()
    {
        // Host the canvas inside a ThemeVariantScope so a parent theme swap
        // propagates ActualThemeVariantChanged down to the canvas.
        var canvas = new NodiumGraphCanvas();
        var scope = new ThemeVariantScope
        {
            RequestedThemeVariant = ThemeVariant.Light,
            Child = canvas,
        };
        var graph = new Graph();
        var nodeA = new Node { X = 100, Y = 100 };
        var nodeB = new Node { X = 300, Y = 200 };
        var portOut = new Port(nodeA, new Point(0, 0));
        var portIn = new Port(nodeB, new Point(0, 0));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        var connection = new Connection(portOut, portIn);
        graph.AddConnection(connection);
        canvas.Graph = graph;
        scope.Measure(new Size(CanvasWidth, CanvasHeight));
        scope.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));

        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        scope.RequestedThemeVariant = ThemeVariant.Dark;

        Assert.Equal(0, canvas.ConnectionGeometryCacheCount);
    }
}
