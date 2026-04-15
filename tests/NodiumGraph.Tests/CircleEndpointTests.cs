using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NodiumGraph.Interactions;
using Xunit;

namespace NodiumGraph.Tests;

public class CircleEndpointTests
{
    [Fact]
    public void Constructor_throws_on_zero_or_negative_radius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleEndpoint(radius: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleEndpoint(radius: -1));
    }

    [Fact]
    public void IsFilled_reflects_constructor()
    {
        Assert.True(new CircleEndpoint(filled: true).IsFilled);
        Assert.False(new CircleEndpoint(filled: false).IsFilled);
    }

    [Fact]
    public void Radius_reflects_constructor()
    {
        Assert.Equal(7, new CircleEndpoint(radius: 7).Radius);
    }

    [Fact]
    public void GetInset_equals_diameter()
    {
        Assert.Equal(10, new CircleEndpoint(radius: 5).GetInset(2));
    }

    [Fact]
    public void BuildGeometry_throws_on_zero_direction()
    {
        var circle = new CircleEndpoint();
        Assert.Throws<ArgumentException>(
            () => circle.BuildGeometry(new Point(0, 0), new Vector(0, 0), strokeThickness: 2));
    }

    [AvaloniaTheory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0.7071067811865475, 0.7071067811865475)]
    public void BuildGeometry_tip_lands_on_transformed_origin(double dx, double dy)
    {
        var circle = new CircleEndpoint(radius: 5, filled: true);
        var tip = new Point(50, 50);
        var geo = circle.BuildGeometry(tip, new Vector(dx, dy), strokeThickness: 2);
        var transformed = ((MatrixTransform)geo.Transform!).Value.Transform(new Point(0, 0));
        Assert.Equal(tip.X, transformed.X, precision: 6);
        Assert.Equal(tip.Y, transformed.Y, precision: 6);
    }
}
