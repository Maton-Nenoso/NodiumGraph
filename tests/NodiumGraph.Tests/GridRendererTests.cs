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
}
