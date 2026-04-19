using Avalonia.Media;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class NodePresenterTests
{
    [Fact]
    public void ScaleBoxShadows_returns_source_when_empty()
    {
        var empty = new BoxShadows();

        var scaled = NodePresenter.ScaleBoxShadows(empty, 0.5);

        Assert.Equal(0, scaled.Count);
    }

    [Fact]
    public void ScaleBoxShadows_returns_source_when_scale_is_one()
    {
        var source = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 1,
            Blur = 3,
            Spread = 0,
            Color = Colors.Black,
        });

        var scaled = NodePresenter.ScaleBoxShadows(source, 1.0);

        Assert.Equal(1, scaled.Count);
        Assert.Equal(source[0], scaled[0]);
    }

    [Fact]
    public void ScaleBoxShadows_scales_offset_blur_and_spread_of_single_shadow()
    {
        var source = new BoxShadows(new BoxShadow
        {
            OffsetX = 2,
            OffsetY = 4,
            Blur = 8,
            Spread = 1,
            Color = Colors.Black,
        });

        var scaled = NodePresenter.ScaleBoxShadows(source, 0.5);

        Assert.Equal(1, scaled.Count);
        Assert.Equal(1, scaled[0].OffsetX);
        Assert.Equal(2, scaled[0].OffsetY);
        Assert.Equal(4, scaled[0].Blur);
        Assert.Equal(0.5, scaled[0].Spread);
        Assert.Equal(Colors.Black, scaled[0].Color);
    }

    [Fact]
    public void ScaleBoxShadows_preserves_inset_flag_and_color()
    {
        var source = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 0,
            Blur = 4,
            Spread = 0,
            Color = Color.FromArgb(0x1A, 0, 0, 0),
            IsInset = true,
        });

        var scaled = NodePresenter.ScaleBoxShadows(source, 0.25);

        Assert.True(scaled[0].IsInset);
        Assert.Equal(Color.FromArgb(0x1A, 0, 0, 0), scaled[0].Color);
        Assert.Equal(1, scaled[0].Blur);
    }

    [Fact]
    public void ScaleBoxShadows_scales_every_shadow_in_multi_shadow_list()
    {
        var first = new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 3, Spread = 0, Color = Colors.Black };
        var second = new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 2, Spread = -1, Color = Colors.Black };
        var source = new BoxShadows(first, [second]);

        var scaled = NodePresenter.ScaleBoxShadows(source, 0.5);

        Assert.Equal(2, scaled.Count);
        Assert.Equal(0.5, scaled[0].OffsetY);
        Assert.Equal(1.5, scaled[0].Blur);
        Assert.Equal(0.5, scaled[1].OffsetY);
        Assert.Equal(1, scaled[1].Blur);
        Assert.Equal(-0.5, scaled[1].Spread);
    }
}
