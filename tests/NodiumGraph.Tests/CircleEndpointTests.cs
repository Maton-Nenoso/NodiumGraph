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

    [AvaloniaFact]
    public void BuildGeometry_rotates_canonical_points_correctly()
    {
        // Arrange: direction pointing +Y (90 degrees CCW from canonical +X).
        // A 90 degree CCW rotation sends (x, y) -> (-y, x); after translating to the tip,
        // a canonical point (cx, cy) lands at (tip.X - cy, tip.Y + cx). The top-left corner
        // of the canonical ellipse bounding rect is asymmetric enough to catch Atan2 swaps.
        var circle = new CircleEndpoint(radius: 5, filled: true);
        var tip = new Point(100, 100);
        var direction = new Vector(0, 1);
        var geo = circle.BuildGeometry(tip, direction, strokeThickness: 2);
        var transform = ((MatrixTransform)geo.Transform!).Value;

        // Top-left corner of the canonical ellipse bounding rect.
        var canonical = new Point(-circle.Radius, -circle.Radius);
        var expected = new Point(tip.X - canonical.Y, tip.Y + canonical.X);

        var actual = transform.Transform(canonical);
        Assert.Equal(expected.X, actual.X, precision: 6);
        Assert.Equal(expected.Y, actual.Y, precision: 6);
    }
}
