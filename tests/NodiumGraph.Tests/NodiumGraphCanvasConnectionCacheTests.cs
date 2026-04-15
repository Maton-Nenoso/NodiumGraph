using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NodiumGraph.Controls;
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
}
