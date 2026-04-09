using Avalonia;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class GridRendererTests
{
    [Fact]
    public void ComputeGridPoints_returns_points_within_visible_area()
    {
        var transform = new ViewportTransform(1.0, default);
        var visibleArea = new Rect(0, 0, 100, 100);

        var points = GridRenderer.ComputeGridPoints(visibleArea, transform, gridSize: 20.0);

        Assert.All(points, p =>
        {
            Assert.InRange(p.X, -1, 101);
            Assert.InRange(p.Y, -1, 101);
        });
        Assert.True(points.Count > 0);
    }

    [Fact]
    public void ComputeGridPoints_respects_zoom()
    {
        var visibleArea = new Rect(0, 0, 200, 200);

        var transform1 = new ViewportTransform(1.0, default);
        var points1 = GridRenderer.ComputeGridPoints(visibleArea, transform1, gridSize: 20.0);

        var transform2 = new ViewportTransform(2.0, default);
        var points2 = GridRenderer.ComputeGridPoints(visibleArea, transform2, gridSize: 20.0);

        // At 2x zoom, world area is smaller so fewer grid points
        Assert.True(points2.Count <= points1.Count);
    }

    [Fact]
    public void ComputeGridPoints_respects_offset()
    {
        var transform = new ViewportTransform(1.0, new Point(100, 100));
        var visibleArea = new Rect(0, 0, 200, 200);

        var points = GridRenderer.ComputeGridPoints(visibleArea, transform, gridSize: 20.0);

        Assert.True(points.Count > 0);
    }

    [Fact]
    public void ComputeGridPoints_returns_empty_for_zero_grid_size()
    {
        var transform = new ViewportTransform(1.0, default);
        var visibleArea = new Rect(0, 0, 100, 100);
        var points = GridRenderer.ComputeGridPoints(visibleArea, transform, gridSize: 0.0);
        Assert.Empty(points);
    }

    [Fact]
    public void ComputeGridPoints_returns_empty_for_negative_grid_size()
    {
        var transform = new ViewportTransform(1.0, default);
        var visibleArea = new Rect(0, 0, 100, 100);
        var points = GridRenderer.ComputeGridPoints(visibleArea, transform, gridSize: -5.0);
        Assert.Empty(points);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(0.3, 1.0)]
    [InlineData(0.2, 0.5)]
    [InlineData(0.1, 0.0)]
    [InlineData(0.05, 0.0)]
    public void ComputeFadeOpacity_returns_expected_value(double zoom, double expected)
    {
        var result = GridRenderer.ComputeFadeOpacity(zoom);
        Assert.Equal(expected, result, precision: 2);
    }

    [Theory]
    [InlineData(20.0, 1.0, 20.0)]   // Normal zoom, no change
    [InlineData(20.0, 0.5, 40.0)]   // Zoomed out: 20*0.5=10 < 15, double to 40*0.5=20
    [InlineData(20.0, 0.2, 80.0)]   // Very zoomed out: need 80*0.2=16 >= 15
    [InlineData(20.0, 4.0, 20.0)]   // Zoomed in: 20*4=80 > 60, but can't halve below base
    [InlineData(50.0, 2.0, 50.0)]   // Zoomed in: 50*2=100 > 60, but can't halve below base
    public void ComputeEffectiveGridSize_returns_expected(double gridSize, double zoom, double expected)
    {
        var result = GridRenderer.ComputeEffectiveGridSize(gridSize, zoom);
        Assert.Equal(expected, result, precision: 1);
    }
}
