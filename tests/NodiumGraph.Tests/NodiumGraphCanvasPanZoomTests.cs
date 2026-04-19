using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasPanZoomTests
{
    [AvaloniaFact]
    public void ViewportOffset_defaults_to_origin()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(default(Point), canvas.ViewportOffset);
    }

    [AvaloniaFact]
    public void ViewportZoom_defaults_to_1()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(1.0, canvas.ViewportZoom);
    }

    [AvaloniaFact]
    public void ViewportOffset_can_be_set_programmatically()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ViewportOffset = new Point(100, 200);
        Assert.Equal(new Point(100, 200), canvas.ViewportOffset);
    }

    [AvaloniaFact]
    public void ViewportZoom_can_be_set_programmatically()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ViewportZoom = 2.5;
        Assert.Equal(2.5, canvas.ViewportZoom);
    }

    [AvaloniaFact]
    public void ViewportZoom_stores_value_without_clamping()
    {
        var canvas = new NodiumGraphCanvas { MinZoom = 0.5 };
        canvas.ViewportZoom = 0.1;
        // The property itself doesn't clamp — clamping happens in wheel handler
        Assert.Equal(0.1, canvas.ViewportZoom);
    }

    [AvaloniaFact]
    public void Pan_state_starts_inactive()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.IsPanning);
    }

    [AvaloniaFact]
    public void Wheel_zoom_changes_zoom_when_not_panning()
    {
        var canvas = new NodiumGraphCanvas();
        var before = canvas.ViewportZoom;

        canvas.HandleWheelZoom(new Point(100, 100), 1.0);

        Assert.NotEqual(before, canvas.ViewportZoom);
    }

    [AvaloniaFact]
    public void Wheel_zoom_is_ignored_while_panning()
    {
        var canvas = new NodiumGraphCanvas { IsPanning = true };
        var zoomBefore = canvas.ViewportZoom;
        var offsetBefore = canvas.ViewportOffset;

        canvas.HandleWheelZoom(new Point(100, 100), 1.0);
        canvas.HandleWheelZoom(new Point(100, 100), -1.0);

        Assert.Equal(zoomBefore, canvas.ViewportZoom);
        Assert.Equal(offsetBefore, canvas.ViewportOffset);
    }
}
