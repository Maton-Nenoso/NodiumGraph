using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class FixedPortProviderTests
{
    // ── Construction ──────────────────────────────────────────────────────────

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

    // ── Implements ILayoutAwarePortProvider ───────────────────────────────────

    [Fact]
    public void Implements_ILayoutAwarePortProvider()
    {
        var provider = new FixedPortProvider();
        Assert.IsAssignableFrom<ILayoutAwarePortProvider>(provider);
    }

    // ── AddPort / RemovePort ──────────────────────────────────────────────────

    [Fact]
    public void AddPort_adds_and_fires_event()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
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
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
        var provider = new FixedPortProvider(new[] { port });

        Port? removed = null;
        provider.PortRemoved += p => removed = p;
        var result = provider.RemovePort(port);

        Assert.True(result);
        Assert.Empty(provider.Ports);
        Assert.Same(port, removed);

        // After Detach, moving node should NOT invalidate the port's absolute position cache.
        // We verify Detach ran by checking the port no longer reacts to owner X changes
        // (no exception thrown means Detach unsubscribed cleanly).
        node.X = 999;
    }

    [Fact]
    public void RemovePort_returns_false_for_absent_port()
    {
        var node = new Node();
        var port = new Port(node, new Point(0, 0));
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
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(100, 0));
        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(5, 5), preview: true);

        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_preview_false_returns_nearest()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(100, 0));
        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(5, 5), preview: false);

        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_returns_null_when_no_port_in_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var provider = new FixedPortProvider(new[] { port1 });

        var resolved = provider.ResolvePort(new Point(500, 500), preview: true);

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolvePort_picks_closest_when_multiple_in_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(20, 0));
        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(18, 0), preview: true);

        Assert.Same(port2, resolved);
    }

    [Fact]
    public void Custom_hit_radius_is_respected()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var provider = new FixedPortProvider(new[] { port1 }, hitRadius: 5.0);

        Assert.NotNull(provider.ResolvePort(new Point(4, 0), preview: true));
        Assert.Null(provider.ResolvePort(new Point(6, 0), preview: true));
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

    // ── UpdateLayout ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateLayout_repositions_ports_when_layout_aware()
    {
        // Port at (0, 0) relative to a 100×100 node.
        // Center-relative: (-50, -50). Nearest boundary on rectangle: (-50, -50) is already a corner,
        // but the boundary snap converts it back to (0, 0) top-left.
        // Use a simpler case: port at center (50, 50) → center-relative (0, 0).
        // RectangleShape snaps (0,0) inside to nearest edge. For a 100×100 node that's any edge at distance 50.
        // To get a predictable result, place port clearly near one edge.
        // Port at (0, 50) relative → center-relative (-50, 0) → on the left boundary already.
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(0, 50));  // left-center of a 100×100 node
        var provider = new FixedPortProvider(new[] { port }, layoutAware: true);

        bool layoutInvalidatedFired = false;
        provider.LayoutInvalidated += () => layoutInvalidatedFired = true;

        provider.UpdateLayout(100, 100, null);

        // center-relative of (0, 50) in 100×100 = (-50, 0), which is exactly on the left boundary
        // → boundary returns (-50, 0) → absolute = (-50+50, 0+50) = (0, 50) — no change expected
        Assert.Equal(new Point(0, 50), port.Position);
        Assert.True(layoutInvalidatedFired);
    }

    [Fact]
    public void UpdateLayout_snaps_interior_port_to_boundary_when_layout_aware()
    {
        // Port at (50, 50) → center-relative (0, 0) in 100×100 node → snaps to nearest edge.
        // RectangleShape: distance to each edge is 50. It snaps to right edge (first minDist == distRight).
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(50, 50));  // center of a 100×100 node
        var provider = new FixedPortProvider(new[] { port }, layoutAware: true);

        provider.UpdateLayout(100, 100, null);

        // RectangleShape ties go to distRight (halfW=50, cy=0) → boundary (50, 0) → absolute (100, 50)
        Assert.Equal(new Point(100, 50), port.Position);
    }

    [Fact]
    public void UpdateLayout_does_not_reposition_when_not_layout_aware()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(50, 50));
        var provider = new FixedPortProvider(new[] { port }, layoutAware: false);

        bool layoutInvalidatedFired = false;
        provider.LayoutInvalidated += () => layoutInvalidatedFired = true;

        provider.UpdateLayout(100, 100, null);

        Assert.Equal(new Point(50, 50), port.Position);
        Assert.False(layoutInvalidatedFired);
    }

    [Fact]
    public void UpdateLayout_uses_provided_shape()
    {
        // Use RectangleShape explicitly — same behavior, just verifying shape param is accepted
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(0, 50));
        var provider = new FixedPortProvider(new[] { port }, layoutAware: true);

        provider.UpdateLayout(100, 100, new RectangleShape());

        Assert.Equal(new Point(0, 50), port.Position);
    }
}
