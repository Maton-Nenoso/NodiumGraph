using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class FixedPortProviderTests
{
    [Fact]
    public void Ports_returns_declared_ports()
    {
        var node = new Node();
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(100, 0));
        var provider = new FixedPortProvider(new[] { port1, port2 });
        Assert.Equal(2, provider.Ports.Count);
        Assert.Contains(port1, provider.Ports);
        Assert.Contains(port2, provider.Ports);
    }

    [Fact]
    public void ResolvePort_returns_nearest_port_within_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(100, 0));
        var provider = new FixedPortProvider(new[] { port1, port2 });
        var resolved = provider.ResolvePort(new Point(5, 5));
        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_returns_null_when_no_port_in_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var provider = new FixedPortProvider(new[] { port1 });
        var resolved = provider.ResolvePort(new Point(500, 500));
        Assert.Null(resolved);
    }

    [Fact]
    public void ResolvePort_picks_closest_when_multiple_in_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(20, 0));
        var provider = new FixedPortProvider(new[] { port1, port2 });
        var resolved = provider.ResolvePort(new Point(18, 0));
        Assert.Same(port2, resolved);
    }

    [Fact]
    public void Custom_hit_radius_is_respected()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var provider = new FixedPortProvider(new[] { port1 }, hitRadius: 5.0);
        Assert.NotNull(provider.ResolvePort(new Point(4, 0)));
        Assert.Null(provider.ResolvePort(new Point(6, 0)));
    }

    [Fact]
    public void Empty_ports_list_is_valid()
    {
        var provider = new FixedPortProvider(Array.Empty<Port>());
        Assert.Empty(provider.Ports);
        Assert.Null(provider.ResolvePort(new Point(0, 0)));
    }

    [Fact]
    public void Constructor_throws_on_null_ports()
    {
        Assert.Throws<ArgumentNullException>(() => new FixedPortProvider(null!));
    }

    [Fact]
    public void Constructor_throws_on_zero_hitRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedPortProvider(Array.Empty<Port>(), hitRadius: 0));
    }

    [Fact]
    public void Constructor_throws_on_negative_hitRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedPortProvider(Array.Empty<Port>(), hitRadius: -5));
    }
}
