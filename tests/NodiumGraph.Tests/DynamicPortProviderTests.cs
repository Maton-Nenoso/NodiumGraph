using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class DynamicPortProviderTests
{
    [Fact]
    public void Initially_has_no_ports()
    {
        var node = new Node();
        node.Width = 100;
        node.Height = 50;
        var provider = new DynamicPortProvider(node);
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void ResolvePort_creates_port_at_boundary()
    {
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 50;
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(110, 25));

        Assert.NotNull(port);
        Assert.Same(node, port!.Owner);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_reuses_existing_port_within_threshold()
    {
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 50;
        var provider = new DynamicPortProvider(node);

        var port1 = provider.ResolvePort(new Point(110, 25));
        var port2 = provider.ResolvePort(new Point(112, 26));

        Assert.Same(port1, port2);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_creates_new_port_beyond_threshold()
    {
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 50;
        var provider = new DynamicPortProvider(node);

        var port1 = provider.ResolvePort(new Point(110, 0));
        var port2 = provider.ResolvePort(new Point(110, 50));

        Assert.NotSame(port1, port2);
        Assert.Equal(2, provider.Ports.Count);
    }

    [Fact]
    public void ResolvePort_returns_null_when_position_far_from_node()
    {
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 50;
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(500, 500));

        Assert.Null(port);
    }

    [Fact]
    public void ResolvePort_returns_null_for_zero_size_node()
    {
        var node = new Node { X = 0, Y = 0 };
        // Width and Height default to 0 (unmeasured node)
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(0, 0));

        Assert.Null(port);
    }

    [Fact]
    public void Constructor_throws_on_null_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new DynamicPortProvider(null!));
    }

    [Fact]
    public void Constructor_throws_on_zero_reuseThreshold()
    {
        var node = new Node();
        Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicPortProvider(node, reuseThreshold: 0));
    }

    [Fact]
    public void Constructor_throws_on_negative_maxDistance()
    {
        var node = new Node();
        Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicPortProvider(node, maxDistance: -1));
    }
}
