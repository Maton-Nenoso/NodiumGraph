using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionHitTesterTests
{
    private static Connection MakeConnection(
        double ax, double ay, double bx, double by,
        Point? sourceLocal = null, Point? targetLocal = null)
    {
        var nodeA = new Node { X = ax, Y = ay };
        var nodeB = new Node { X = bx, Y = by };
        var source = new Port(nodeA, sourceLocal ?? new Point(0, 0));
        var target = new Port(nodeB, targetLocal ?? new Point(0, 0));
        return new Connection(source, target);
    }

    private static CachedConnectionGeometry BuildCacheEntry(
        Connection connection, IConnectionStyle style)
    {
        var router = new StraightRouter();
        var renderable = ConnectionRenderer.CreateRenderable(connection, router, style);
        return new CachedConnectionGeometry(renderable, Version: 0);
    }

    private static Func<Connection, IConnectionStyle> ConstantStyle(IConnectionStyle style)
        => _ => style;

    [AvaloniaFact]
    public void HitTest_on_straight_line_returns_connection()
    {
        // Straight route from world (0,0) to (100,0).
        var connection = MakeConnection(0, 0, 100, 0);
        var style = new ConnectionStyle();
        var cache = new Dictionary<Guid, CachedConnectionGeometry>
        {
            [connection.Id] = BuildCacheEntry(connection, style),
        };

        var hit = ConnectionHitTester.HitTest(
            new Point(50, 0), worldTolerance: 2,
            new[] { connection }, ConstantStyle(style), cache);

        Assert.Same(connection, hit);
    }

    [AvaloniaFact]
    public void HitTest_beyond_tolerance_returns_null()
    {
        var connection = MakeConnection(0, 0, 100, 0);
        var style = new ConnectionStyle();
        var cache = new Dictionary<Guid, CachedConnectionGeometry>
        {
            [connection.Id] = BuildCacheEntry(connection, style),
        };

        var hit = ConnectionHitTester.HitTest(
            new Point(50, 20), worldTolerance: 2,
            new[] { connection }, ConstantStyle(style), cache);

        Assert.Null(hit);
    }

    [AvaloniaFact]
    public void HitTest_on_filled_arrow_interior_via_FillContains()
    {
        // Long horizontal route; a filled arrow of size 20 at the target.
        // The arrow's filled interior sits a few pixels back from the tip along the axis.
        var connection = MakeConnection(0, 0, 300, 0);
        var style = new ConnectionStyle(
            thickness: 1,
            targetEndpoint: new ArrowEndpoint(size: 20, filled: true));
        var cache = new Dictionary<Guid, CachedConnectionGeometry>
        {
            [connection.Id] = BuildCacheEntry(connection, style),
        };

        // A point 5 world-units off the line, close to the arrow tip — outside a
        // thin stroke hit region but inside the filled arrow geometry.
        var hit = ConnectionHitTester.HitTest(
            new Point(290, 5), worldTolerance: 1,
            new[] { connection }, ConstantStyle(style), cache);

        Assert.Same(connection, hit);
    }

    [AvaloniaFact]
    public void HitTest_overlapping_connections_returns_topmost()
    {
        // Two connections cross at (50, 50).
        // First: diagonal (0,0) -> (100,100).
        // Second: diagonal (0,100) -> (100,0).
        var first = MakeConnection(0, 0, 100, 100);
        var second = MakeConnection(0, 100, 100, 0);
        var style = new ConnectionStyle();
        var cache = new Dictionary<Guid, CachedConnectionGeometry>
        {
            [first.Id] = BuildCacheEntry(first, style),
            [second.Id] = BuildCacheEntry(second, style),
        };

        var connections = new[] { first, second };

        var hit = ConnectionHitTester.HitTest(
            new Point(50, 50), worldTolerance: 5,
            connections, ConstantStyle(style), cache);

        Assert.Same(second, hit);
    }

    [AvaloniaFact]
    public void HitTest_tolerance_scales_with_parameter()
    {
        var connection = MakeConnection(0, 0, 100, 0);
        var style = new ConnectionStyle(thickness: 1);
        var cache = new Dictionary<Guid, CachedConnectionGeometry>
        {
            [connection.Id] = BuildCacheEntry(connection, style),
        };

        var point = new Point(50, 4);

        var missed = ConnectionHitTester.HitTest(
            point, worldTolerance: 2,
            new[] { connection }, ConstantStyle(style), cache);
        Assert.Null(missed);

        var hit = ConnectionHitTester.HitTest(
            point, worldTolerance: 8,
            new[] { connection }, ConstantStyle(style), cache);
        Assert.Same(connection, hit);
    }

    [AvaloniaFact]
    public void HitTest_skips_connections_not_in_cache()
    {
        var connection = MakeConnection(0, 0, 100, 0);
        var style = new ConnectionStyle();
        var cache = new Dictionary<Guid, CachedConnectionGeometry>();

        var hit = ConnectionHitTester.HitTest(
            new Point(50, 0), worldTolerance: 2,
            new[] { connection }, ConstantStyle(style), cache);

        Assert.Null(hit);
    }

    [AvaloniaFact]
    public void HitTest_empty_connection_list_returns_null()
    {
        var style = new ConnectionStyle();
        var cache = new Dictionary<Guid, CachedConnectionGeometry>();

        var hit = ConnectionHitTester.HitTest(
            new Point(50, 0), worldTolerance: 2,
            Array.Empty<Connection>(), ConstantStyle(style), cache);

        Assert.Null(hit);
    }
}
