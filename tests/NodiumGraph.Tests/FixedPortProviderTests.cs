using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class FixedPortProviderTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Ports_returns_declared_ports()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port1 = TestNodes.PortAt(node, 0, 25);
        var port2 = TestNodes.PortAt(node, 100, 25);
        var provider = new FixedPortProvider(new[] { port1, port2 });
        Assert.Equal(2, provider.Ports.Count);
        Assert.Contains(port1, provider.Ports);
        Assert.Contains(port2, provider.Ports);
    }

    [Fact]
    public void Parameterless_constructor_creates_empty_provider()
    {
        var provider = new FixedPortProvider();
        Assert.Empty(provider.Ports);
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

    [Fact]
    public void Empty_ports_list_is_valid()
    {
        var provider = new FixedPortProvider(Array.Empty<Port>());
        Assert.Empty(provider.Ports);
        Assert.Null(provider.ResolvePort(new Point(0, 0), preview: true));
    }

    [Fact]
    public void Constructor_with_null_element_throws()
    {
        var ports = new Port[] { null! };
        Assert.Throws<ArgumentNullException>(() => new FixedPortProvider(ports));
    }

    // ── AddPort / RemovePort ──────────────────────────────────────────────────

    [Fact]
    public void AddPort_adds_and_fires_event()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = TestNodes.PortAt(node, 0, 25);
        var provider = new FixedPortProvider();

        Port? received = null;
        provider.PortAdded += p => received = p;
        provider.AddPort(port);

        Assert.Single(provider.Ports);
        Assert.Same(port, provider.Ports[0]);
        Assert.Same(port, received);
    }

    [Fact]
    public void AddPort_throws_on_null()
    {
        var provider = new FixedPortProvider();
        Assert.Throws<ArgumentNullException>(() => provider.AddPort(null!));
    }

    [Fact]
    public void RemovePort_removes_fires_event_and_detaches()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = TestNodes.PortAt(node, 0, 25);
        var provider = new FixedPortProvider(new[] { port });

        Port? removed = null;
        provider.PortRemoved += p => removed = p;
        var result = provider.RemovePort(port);

        Assert.True(result);
        Assert.Empty(provider.Ports);
        Assert.Same(port, removed);

        // After Detach, moving node should NOT invalidate the port's absolute position cache.
        node.X = 999;
    }

    [Fact]
    public void RemovePort_returns_false_for_absent_port()
    {
        var node = new Node { Width = 100, Height = 50 };
        var port = TestNodes.PortAt(node, 0, 25);
        var provider = new FixedPortProvider();

        var result = provider.RemovePort(port);

        Assert.False(result);
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void RemovePort_throws_on_null()
    {
        var provider = new FixedPortProvider();
        Assert.Throws<ArgumentNullException>(() => provider.RemovePort(null!));
    }

    // ── ResolvePort ───────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePort_preview_true_returns_nearest()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port1 = TestNodes.PortAt(node, 0, 25);   // left edge → abs (0, 25)
        var port2 = TestNodes.PortAt(node, 100, 25); // right edge → abs (100, 25)
        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(5, 25), preview: true);

        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_preview_false_returns_nearest()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port1 = TestNodes.PortAt(node, 0, 25);
        var port2 = TestNodes.PortAt(node, 100, 25);
        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(5, 25), preview: false);

        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_returns_null_when_no_port_in_radius()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port1 = TestNodes.PortAt(node, 0, 25);
        var provider = new FixedPortProvider(new[] { port1 });

        var resolved = provider.ResolvePort(new Point(500, 500), preview: true);

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolvePort_picks_closest_when_multiple_in_radius()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port1 = TestNodes.PortAt(node, 0, 25);
        var port2 = TestNodes.PortAt(node, 20, 0);
        // port2 absolute position ≈ (20, 0); hit at (18, 0) is closer to port2 than port1 at (0, 25)
        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(18, 0), preview: true);

        Assert.Same(port2, resolved);
    }

    [Fact]
    public void Custom_hit_radius_is_respected()
    {
        var node = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
        var port1 = TestNodes.PortAt(node, 0, 25);
        var provider = new FixedPortProvider(new[] { port1 }, hitRadius: 5.0);

        Assert.NotNull(provider.ResolvePort(new Point(4, 25), preview: true));
        Assert.Null(provider.ResolvePort(new Point(6, 25), preview: true));
    }

    // ── CancelResolve ─────────────────────────────────────────────────────────

    [Fact]
    public void CancelResolve_is_noop()
    {
        var provider = new FixedPortProvider();
        // Should not throw and leave provider in valid state
        provider.CancelResolve();
        Assert.Empty(provider.Ports);
    }
}
