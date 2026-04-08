using NodiumGraph.Interactions;
using Avalonia.Media;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionStyleTests
{
    [Fact]
    public void Default_style_has_sensible_defaults()
    {
        var style = new ConnectionStyle();
        Assert.NotNull(style.Stroke);
        Assert.True(style.Thickness > 0);
        Assert.Null(style.DashPattern);
    }

    [Fact]
    public void Custom_style_preserves_values()
    {
        var brush = Brushes.Red;
        var dash = DashStyle.Dash;
        var style = new ConnectionStyle(brush, 3.0, dash);
        Assert.Same(brush, style.Stroke);
        Assert.Equal(3.0, style.Thickness);
        Assert.Same(dash, style.DashPattern);
    }
}
