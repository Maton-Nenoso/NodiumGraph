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

    [Fact]
    public void Zero_thickness_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionStyle(thickness: 0));
    }

    [Fact]
    public void Negative_thickness_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionStyle(thickness: -1));
    }

    [Fact]
    public void Default_endpoints_are_null()
    {
        var style = new ConnectionStyle();
        Assert.Null(style.SourceEndpoint);
        Assert.Null(style.TargetEndpoint);
    }

    [Fact]
    public void TargetEndpoint_round_trips_through_constructor()
    {
        var arrow = new ArrowEndpoint();
        var style = new ConnectionStyle(targetEndpoint: arrow);
        Assert.Same(arrow, style.TargetEndpoint);
        Assert.Null(style.SourceEndpoint);
    }

    [Fact]
    public void SourceEndpoint_round_trips_through_constructor()
    {
        var arrow = new ArrowEndpoint();
        var style = new ConnectionStyle(sourceEndpoint: arrow);
        Assert.Same(arrow, style.SourceEndpoint);
        Assert.Null(style.TargetEndpoint);
    }
}
