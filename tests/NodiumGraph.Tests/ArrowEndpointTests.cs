using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Interactions;
using Xunit;

namespace NodiumGraph.Tests;

public class ArrowEndpointTests
{
    [Fact]
    public void Constructor_throws_on_zero_or_negative_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrowEndpoint(size: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrowEndpoint(size: -1));
    }

    [Fact]
    public void GetInset_filled_equals_size()
    {
        var endpoint = new ArrowEndpoint(size: 8, filled: true);
        Assert.Equal(8, endpoint.GetInset(2));
    }

    [Fact]
    public void GetInset_open_is_90_percent_of_size()
    {
        var endpoint = new ArrowEndpoint(size: 8, filled: false);
        Assert.Equal(7.2, endpoint.GetInset(2), precision: 3);
    }

    [Fact]
    public void IsFilled_reflects_constructor()
    {
        Assert.True(new ArrowEndpoint(filled: true).IsFilled);
        Assert.False(new ArrowEndpoint(filled: false).IsFilled);
    }

    [AvaloniaTheory]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(-1.0, 0.0)]
    [InlineData(0.0, -1.0)]
    public void BuildGeometry_points_tip_at_expected_position(double dx, double dy)
    {
        var endpoint = new ArrowEndpoint(size: 8, filled: true);
        var tip = new Point(50, 50);

        var geo = endpoint.BuildGeometry(tip, new Vector(dx, dy), strokeThickness: 2);

        Assert.True(geo.Bounds.Contains(new Point(50, 50)));
    }
}
