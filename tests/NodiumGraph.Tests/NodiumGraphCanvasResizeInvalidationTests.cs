using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
using Xunit;

namespace NodiumGraph.Tests;

/// <summary>
/// Pins the invalidation path triggered by node Width, Height, and Shape changes.
/// Resizing a node must drop the cached connection geometry for all connections
/// that touch it so the next render rebuilds with the updated port positions.
/// </summary>
public class NodiumGraphCanvasResizeInvalidationTests
{
    private const int CanvasWidth = 800;
    private const int CanvasHeight = 600;

    private static (NodiumGraphCanvas canvas, Node nodeA, Node nodeB, Connection connection) Build()
    {
        var graph = new Graph();
        var nodeA = new Node { X = 100, Y = 100, Width = 100, Height = 50 };
        var nodeB = new Node { X = 300, Y = 100, Width = 100, Height = 50 };
        var portA = TestNodes.PortAt(nodeA, 100, 25, "out", PortFlow.Output);
        var portB = TestNodes.PortAt(nodeB, 0, 25, "in", PortFlow.Input);
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        var connection = new Connection(portA, portB);
        graph.AddConnection(connection);

        var canvas = new NodiumGraphCanvas { Graph = graph };
        canvas.Measure(new Size(CanvasWidth, CanvasHeight));
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));

        return (canvas, nodeA, nodeB, connection);
    }

    private static void DriveRender(NodiumGraphCanvas canvas)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(CanvasWidth, CanvasHeight));
        using var ctx = bitmap.CreateDrawingContext();
        canvas.Render(ctx);
    }

    [AvaloniaFact]
    public void Width_change_invalidates_connection_geometry_for_touched_node()
    {
        var (canvas, nodeA, _, connection) = Build();
        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        nodeA.Width = 200;

        Assert.False(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Width change must drop cached geometry for connections touching the resized node.");
    }

    [AvaloniaFact]
    public void Height_change_invalidates_connection_geometry_for_touched_node()
    {
        var (canvas, nodeA, _, connection) = Build();
        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        nodeA.Height = 120;

        Assert.False(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Height change must drop cached geometry for connections touching the resized node.");
    }

    [AvaloniaFact]
    public void Shape_swap_invalidates_connection_geometry_for_touched_node()
    {
        var (canvas, nodeA, _, connection) = Build();
        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        nodeA.Shape = new EllipseShape();

        Assert.False(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Shape change must drop cached geometry for connections touching the affected node.");
    }

    [AvaloniaFact]
    public void Resize_of_unrelated_node_does_not_invalidate_unrelated_connection()
    {
        // nodeA–connection–nodeB; nodeC is unrelated.
        var (canvas, _, _, connection) = Build();
        var nodeC = new Node { X = 500, Y = 300, Width = 80, Height = 40 };
        canvas.Graph!.AddNode(nodeC);

        DriveRender(canvas);
        Assert.True(canvas.ConnectionGeometryCacheContains(connection.Id));

        nodeC.Width = 160;

        Assert.True(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Resizing an unrelated node must not invalidate unrelated connections.");
    }

    [AvaloniaFact]
    public void Cache_repopulates_after_width_change_on_next_render()
    {
        var (canvas, nodeA, _, connection) = Build();
        DriveRender(canvas);

        nodeA.Width = 150;
        Assert.False(canvas.ConnectionGeometryCacheContains(connection.Id));

        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));
        DriveRender(canvas);

        Assert.True(
            canvas.ConnectionGeometryCacheContains(connection.Id),
            "Cache must be repopulated after resize on the next render pass.");
    }
}
