using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NodiumGraph.Interactions;
using Xunit;

namespace NodiumGraph.Tests;

public class DiamondEndpointTests
{
    [Fact]
    public void Constructor_throws_on_zero_or_negative_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiamondEndpoint(size: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiamondEndpoint(size: -1));
    }

    [Fact]
    public void IsFilled_reflects_constructor()
    {
        Assert.True(new DiamondEndpoint(filled: true).IsFilled);
        Assert.False(new DiamondEndpoint(filled: false).IsFilled);
    }

    [Fact]
    public void Size_reflects_constructor()
    {
        Assert.Equal(12, new DiamondEndpoint(size: 12).Size);
    }

    [Fact]
    public void GetInset_equals_size_regardless_of_fill()
    {
        Assert.Equal(10, new DiamondEndpoint(size: 10, filled: true).GetInset(2));
        Assert.Equal(10, new DiamondEndpoint(size: 10, filled: false).GetInset(2));
    }

    [Fact]
    public void BuildGeometry_throws_on_zero_direction()
    {
        var diamond = new DiamondEndpoint();
        Assert.Throws<ArgumentException>(
            () => diamond.BuildGeometry(new Point(0, 0), new Vector(0, 0), strokeThickness: 2));
    }

    [AvaloniaTheory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0.7071067811865475, 0.7071067811865475)]
    public void BuildGeometry_tip_lands_on_transformed_origin(double dx, double dy)
    {
        var diamond = new DiamondEndpoint(size: 10, filled: true);
        var tip = new Point(50, 50);
        var geo = diamond.BuildGeometry(tip, new Vector(dx, dy), strokeThickness: 2);
        var transformed = ((MatrixTransform)geo.Transform!).Value.Transform(new Point(0, 0));
        Assert.Equal(tip.X, transformed.X, precision: 6);
        Assert.Equal(tip.Y, transformed.Y, precision: 6);
    }
}
