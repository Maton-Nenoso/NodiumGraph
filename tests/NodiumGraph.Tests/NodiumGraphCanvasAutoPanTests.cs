using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasAutoPanTests
{
    [AvaloniaFact]
    public void AutoPan_does_not_throw_with_zero_bounds()
    {
        var canvas = new NodiumGraphCanvas();
        // Canvas has zero bounds in headless — should not throw
        canvas.ApplyAutoPan(new Point(0, 0));
    }

    [AvaloniaFact]
    public void AutoPan_does_not_change_offset_when_centered()
    {
        var canvas = new NodiumGraphCanvas();
        var original = canvas.ViewportOffset;
        // With zero bounds, auto-pan should be a no-op
        canvas.ApplyAutoPan(new Point(200, 200));
        Assert.Equal(original, canvas.ViewportOffset);
    }
}
