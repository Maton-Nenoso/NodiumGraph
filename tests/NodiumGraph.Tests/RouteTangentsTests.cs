using Avalonia;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using Xunit;

namespace NodiumGraph.Tests;

public class RouteTangentsTests
{
    [Fact]
    public void From_empty_list_returns_default()
    {
        var tangents = RouteTangents.From(System.Array.Empty<Point>(), RouteKind.Polyline);

        Assert.Equal(0.0, tangents.Source.X, precision: 6);
        Assert.Equal(0.0, tangents.Source.Y, precision: 6);
        Assert.Equal(0.0, tangents.Target.X, precision: 6);
        Assert.Equal(0.0, tangents.Target.Y, precision: 6);
    }

    [Fact]
    public void From_single_point_returns_default()
    {
        var points = new[] { new Point(5, 5) };

        var tangents = RouteTangents.From(points, RouteKind.Polyline);

        Assert.Equal(0.0, tangents.Source.X, precision: 6);
        Assert.Equal(0.0, tangents.Source.Y, precision: 6);
        Assert.Equal(0.0, tangents.Target.X, precision: 6);
        Assert.Equal(0.0, tangents.Target.Y, precision: 6);
    }

    [Fact]
    public void From_null_returns_default()
    {
        var tangents = RouteTangents.From(null!, RouteKind.Polyline);

        Assert.Equal(0.0, tangents.Source.X, precision: 6);
        Assert.Equal(0.0, tangents.Source.Y, precision: 6);
        Assert.Equal(0.0, tangents.Target.X, precision: 6);
        Assert.Equal(0.0, tangents.Target.Y, precision: 6);
    }

    [Fact]
    public void Bezier_with_4_points_uses_control_tangents()
    {
        var points = new[]
        {
            new Point(0, 0),
            new Point(10, 0),
            new Point(20, 10),
            new Point(30, 10)
        };

        var tangents = RouteTangents.From(points, RouteKind.Bezier);

        Assert.Equal(1.0, tangents.Source.X, precision: 6);
        Assert.Equal(0.0, tangents.Source.Y, precision: 6);
        Assert.Equal(1.0, tangents.Target.X, precision: 6);
        Assert.Equal(0.0, tangents.Target.Y, precision: 6);
    }

    [Fact]
    public void Polyline_uses_first_and_last_segments()
    {
        var points = new[]
        {
            new Point(0, 0),
            new Point(10, 0),
            new Point(10, 10)
        };

        var tangents = RouteTangents.From(points, RouteKind.Polyline);

        Assert.Equal(1.0, tangents.Source.X, precision: 6);
        Assert.Equal(0.0, tangents.Source.Y, precision: 6);
        Assert.Equal(0.0, tangents.Target.X, precision: 6);
        Assert.Equal(1.0, tangents.Target.Y, precision: 6);
    }

    [Fact]
    public void Bezier_with_non_4_points_falls_back_to_polyline_rule()
    {
        var points = new[]
        {
            new Point(0, 0),
            new Point(5, 5),
            new Point(10, 10)
        };

        var tangents = RouteTangents.From(points, RouteKind.Bezier);

        var expected = 1.0 / System.Math.Sqrt(2);
        Assert.Equal(expected, tangents.Source.X, precision: 6);
        Assert.Equal(expected, tangents.Source.Y, precision: 6);
        Assert.Equal(expected, tangents.Target.X, precision: 6);
        Assert.Equal(expected, tangents.Target.Y, precision: 6);
    }

    [Fact]
    public void Zero_length_first_segment_returns_zero_source()
    {
        var points = new[]
        {
            new Point(5, 5),
            new Point(5, 5),
            new Point(10, 5)
        };

        var tangents = RouteTangents.From(points, RouteKind.Polyline);

        Assert.Equal(0.0, tangents.Source.X, precision: 6);
        Assert.Equal(0.0, tangents.Source.Y, precision: 6);
        Assert.Equal(1.0, tangents.Target.X, precision: 6);
        Assert.Equal(0.0, tangents.Target.Y, precision: 6);
    }
}
