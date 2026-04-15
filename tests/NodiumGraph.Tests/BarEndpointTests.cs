using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NodiumGraph.Interactions;
using Xunit;

namespace NodiumGraph.Tests;

public class BarEndpointTests
{
    [Fact]
    public void Constructor_throws_on_zero_or_negative_width()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarEndpoint(width: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarEndpoint(width: -1));
    }

    [Fact]
    public void Constructor_throws_on_zero_or_negative_length()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarEndpoint(length: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BarEndpoint(length: -1));
    }

    [Fact]
    public void IsFilled_is_always_false()
    {
        Assert.False(new BarEndpoint().IsFilled);
        Assert.False(new BarEndpoint(width: 4, length: 20).IsFilled);
    }

    [Fact]
    public void Width_and_Length_reflect_constructor()
    {
        var bar = new BarEndpoint(width: 3, length: 14);
        Assert.Equal(3, bar.Width);
        Assert.Equal(14, bar.Length);
    }

    [Fact]
    public void GetInset_equals_half_width()
    {
        Assert.Equal(1, new BarEndpoint(width: 2, length: 12).GetInset(2));
        Assert.Equal(2, new BarEndpoint(width: 4, length: 12).GetInset(2));
    }

    [Fact]
    public void BuildGeometry_throws_on_zero_direction()
    {
        var bar = new BarEndpoint();
        Assert.Throws<ArgumentException>(
            () => bar.BuildGeometry(new Point(0, 0), new Vector(0, 0), strokeThickness: 2));
    }

    [AvaloniaTheory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0.7071067811865475, 0.7071067811865475)]
    public void BuildGeometry_tip_lands_on_transformed_origin(double dx, double dy)
    {
        var bar = new BarEndpoint(width: 2, length: 12);
        var tip = new Point(50, 50);
        var geo = bar.BuildGeometry(tip, new Vector(dx, dy), strokeThickness: 2);
        var transformed = ((MatrixTransform)geo.Transform!).Value.Transform(new Point(0, 0));
        Assert.Equal(tip.X, transformed.X, precision: 6);
        Assert.Equal(tip.Y, transformed.Y, precision: 6);
    }
}
