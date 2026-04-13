using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasPropertyTests
{
    [AvaloniaFact]
    public void Graph_defaults_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.Graph);
    }

    [AvaloniaFact]
    public void ViewportZoom_defaults_to_1()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(1.0, canvas.ViewportZoom);
    }

    [AvaloniaFact]
    public void ViewportOffset_defaults_to_origin()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(default(Point), canvas.ViewportOffset);
    }

    [AvaloniaFact]
    public void MinZoom_defaults_to_0_1()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(0.1, canvas.MinZoom);
    }

    [AvaloniaFact]
    public void MaxZoom_defaults_to_5()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(5.0, canvas.MaxZoom);
    }

    [AvaloniaFact]
    public void ShowGrid_defaults_to_true()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.True(canvas.ShowGrid);
    }

    [AvaloniaFact]
    public void GridSize_defaults_to_20()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(20.0, canvas.GridSize);
    }

    [AvaloniaFact]
    public void SnapToGrid_defaults_to_false()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.SnapToGrid);
    }

    [AvaloniaFact]
    public void ShowMinimap_defaults_to_false()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.ShowMinimap);
    }

    [AvaloniaFact]
    public void MinimapPosition_defaults_to_BottomRight()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(MinimapPosition.BottomRight, canvas.MinimapPosition);
    }

    [AvaloniaFact]
    public void Handlers_default_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.NodeHandler);
        Assert.Null(canvas.ConnectionHandler);
        Assert.Null(canvas.SelectionHandler);
        Assert.Null(canvas.CanvasHandler);
    }

    [AvaloniaFact]
    public void ConnectionValidator_defaults_to_DefaultConnectionValidator()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Same(DefaultConnectionValidator.Instance, canvas.ConnectionValidator);
    }

    [AvaloniaFact]
    public void ConnectionRouter_defaults_to_BezierRouter()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.IsType<BezierRouter>(canvas.ConnectionRouter);
    }

    [AvaloniaFact]
    public void NodeTemplate_defaults_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.NodeTemplate);
    }

    [AvaloniaFact]
    public void PortTemplate_defaults_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.PortTemplate);
    }

    [AvaloniaFact]
    public void Graph_can_be_set()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;
        Assert.Same(graph, canvas.Graph);
    }
}
