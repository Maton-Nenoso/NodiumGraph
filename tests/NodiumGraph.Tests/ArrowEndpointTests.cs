using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0.7071067811865475, 0.7071067811865475)] // diagonal
    public void BuildGeometry_tip_lands_on_transformed_origin(double dx, double dy)
    {
        var arrow = new ArrowEndpoint(size: 10, filled: true);
        var tip = new Point(50, 50);
        var geo = arrow.BuildGeometry(tip, new Vector(dx, dy), strokeThickness: 2);
        var transformed = ((MatrixTransform)geo.Transform!).Value.Transform(new Point(0, 0));
        Assert.Equal(tip.X, transformed.X, precision: 6);
        Assert.Equal(tip.Y, transformed.Y, precision: 6);
    }

    [Fact]
    public void BuildGeometry_throws_on_zero_direction()
    {
        var arrow = new ArrowEndpoint();
        Assert.Throws<ArgumentException>(
            () => arrow.BuildGeometry(new Point(0, 0), new Vector(0, 0), strokeThickness: 2));
    }

    [AvaloniaFact]
    public void BuildGeometry_rotates_canonical_points_correctly()
    {
        // Arrange: direction pointing +Y (90 degrees CCW from canonical +X).
        // A 90 degree CCW rotation sends (x, y) -> (-y, x); after translating to the tip,
        // a canonical point (cx, cy) lands at (tip.X - cy, tip.Y + cx). A swapped Atan2 or
        // sign error would land the point elsewhere.
        var arrow = new ArrowEndpoint(size: 10, filled: true);
        var tip = new Point(100, 100);
        var direction = new Vector(0, 1);
        var geo = arrow.BuildGeometry(tip, direction, strokeThickness: 2);
        var transform = ((MatrixTransform)geo.Transform!).Value;

        // Top-left base vertex of the canonical arrow triangle.
        var canonical = new Point(-arrow.Size, -arrow.Size / 2.0);
        var expected = new Point(tip.X - canonical.Y, tip.Y + canonical.X);

        var actual = transform.Transform(canonical);
        Assert.Equal(expected.X, actual.X, precision: 6);
        Assert.Equal(expected.Y, actual.Y, precision: 6);
    }
}
