using Avalonia;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class ViewportTransformTests
{
    [Fact]
    public void Identity_transform_at_default_zoom_and_offset()
    {
        var vt = new ViewportTransform(zoom: 1.0, offset: default);
        var world = new Point(100, 200);
        Assert.Equal(world, vt.WorldToScreen(world));
        Assert.Equal(world, vt.ScreenToWorld(world));
    }

    [Fact]
    public void Offset_shifts_world_to_screen()
    {
        var vt = new ViewportTransform(zoom: 1.0, offset: new Point(50, 100));
        Assert.Equal(new Point(50, 100), vt.WorldToScreen(new Point(0, 0)));
    }

    [Fact]
    public void Screen_to_world_reverses_offset()
    {
        var vt = new ViewportTransform(zoom: 1.0, offset: new Point(50, 100));
        Assert.Equal(new Point(0, 0), vt.ScreenToWorld(new Point(50, 100)));
    }

    [Fact]
    public void Zoom_scales_world_to_screen()
    {
        var vt = new ViewportTransform(zoom: 2.0, offset: default);
        Assert.Equal(new Point(200, 400), vt.WorldToScreen(new Point(100, 200)));
    }

    [Fact]
    public void Screen_to_world_reverses_zoom()
    {
        var vt = new ViewportTransform(zoom: 2.0, offset: default);
        Assert.Equal(new Point(100, 200), vt.ScreenToWorld(new Point(200, 400)));
    }

    [Fact]
    public void Zoom_and_offset_combined()
    {
        var vt = new ViewportTransform(zoom: 2.0, offset: new Point(10, 20));
        Assert.Equal(new Point(110, 220), vt.WorldToScreen(new Point(50, 100)));
        Assert.Equal(new Point(50, 100), vt.ScreenToWorld(new Point(110, 220)));
    }

    [Fact]
    public void ScreenToWorld_with_zero_zoom_returns_input()
    {
        var vt = new ViewportTransform(zoom: 0, offset: default);
        var screen = new Point(100, 200);
        Assert.Equal(screen, vt.ScreenToWorld(screen));
    }

    [Fact]
    public void ScreenToWorld_length_with_zero_zoom_returns_input()
    {
        var vt = new ViewportTransform(zoom: 0, offset: default);
        Assert.Equal(42.0, vt.ScreenToWorld(42.0));
    }

    [Fact]
    public void Roundtrip_preserves_point()
    {
        var vt = new ViewportTransform(zoom: 1.5, offset: new Point(37, -42));
        var original = new Point(123.456, -789.012);
        var roundtrip = vt.ScreenToWorld(vt.WorldToScreen(original));
        Assert.Equal(original.X, roundtrip.X, 6);
        Assert.Equal(original.Y, roundtrip.Y, 6);
    }
}
