using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class DynamicPortProviderTests
{
    // Helper: create a node with Width/Height set
    private static Node MakeNode(double x = 0, double y = 0, double width = 100, double height = 50)
    {
        var node = new Node { X = x, Y = y };
        node.Width = width;
        node.Height = height;
        return node;
    }

    [Fact]
    public void Initially_has_no_ports()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void ResolvePort_creates_port_at_boundary()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(110, 25), preview: false);

        Assert.NotNull(port);
        Assert.Same(node, port!.Owner);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_reuses_existing_port_within_threshold()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        var port1 = provider.ResolvePort(new Point(110, 25), preview: false);
        var port2 = provider.ResolvePort(new Point(112, 26), preview: false);

        Assert.Same(port1, port2);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_creates_new_port_beyond_threshold()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        var port1 = provider.ResolvePort(new Point(110, 0), preview: false);
        var port2 = provider.ResolvePort(new Point(110, 50), preview: false);

        Assert.NotSame(port1, port2);
        Assert.Equal(2, provider.Ports.Count);
    }

    [Fact]
    public void ResolvePort_returns_null_when_position_far_from_node()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(500, 500), preview: false);

        Assert.Null(port);
    }

    [Fact]
    public void ResolvePort_returns_null_for_zero_size_node()
    {
        var node = new Node { X = 0, Y = 0 };
        // Width and Height default to 0 (unmeasured node)
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(0, 0), preview: false);

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

    [Fact]
    public void CancelResolve_removes_last_created_port()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        provider.ResolvePort(new Point(110, 25), preview: false);
        Assert.Single(provider.Ports);

        provider.CancelResolve();

        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void CancelResolve_fires_PortRemoved()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        Port? removed = null;
        provider.PortRemoved += p => removed = p;

        var port = provider.ResolvePort(new Point(110, 25), preview: false);
        provider.CancelResolve();

        Assert.Same(port, removed);
    }

    [Fact]
    public void Preview_true_does_not_create_port()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(110, 25), preview: true);

        Assert.Null(port);
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void Preview_true_returns_existing_port_near_position()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        // First commit to create the port
        var committed = provider.ResolvePort(new Point(110, 25), preview: false);
        // Now preview near the same spot should return the existing port
        var previewed = provider.ResolvePort(new Point(112, 26), preview: true);

        Assert.NotNull(previewed);
        Assert.Same(committed, previewed);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_commit_fires_PortAdded()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        Port? added = null;
        provider.PortAdded += p => added = p;

        var port = provider.ResolvePort(new Point(110, 25), preview: false);

        Assert.Same(port, added);
    }

    [Fact]
    public void PruneUnconnected_removes_ports_with_no_connections()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);
        var graph = new Graph();
        graph.AddNode(node);

        // Create two ports
        var port1 = provider.ResolvePort(new Point(110, 10), preview: false)!;
        var port2 = provider.ResolvePort(new Point(110, 40), preview: false)!;

        // Connect port1 to a port on another node, leave port2 unconnected
        var other = MakeNode(x: 300);
        var otherPort = new Port(other, new Point(0, 25));
        graph.AddNode(other);
        graph.AddConnection(new Connection(port1, otherPort));

        provider.PruneUnconnected(graph);

        Assert.Single(provider.Ports);
        Assert.Same(port1, provider.Ports[0]);
    }

    [Fact]
    public void NotifyDisconnected_removes_port_when_auto_prune_enabled()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node) { AutoPruneOnDisconnect = true };
        var graph = new Graph();
        graph.AddNode(node);

        var port = provider.ResolvePort(new Point(110, 25), preview: false)!;
        // No connections — calling NotifyDisconnected should prune it
        provider.NotifyDisconnected(port, graph);

        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void NotifyDisconnected_keeps_port_when_auto_prune_disabled()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node) { AutoPruneOnDisconnect = false };
        var graph = new Graph();
        graph.AddNode(node);

        var port = provider.ResolvePort(new Point(110, 25), preview: false)!;
        provider.NotifyDisconnected(port, graph);

        Assert.Single(provider.Ports);
    }

    [Fact]
    public void CancelResolve_noop_when_reusing_existing()
    {
        var node = MakeNode();
        var provider = new DynamicPortProvider(node);

        // Commit a port to create it
        var port = provider.ResolvePort(new Point(110, 25), preview: false)!;
        Assert.Single(provider.Ports);

        // Reuse the same port in commit mode — this clears _lastCreated
        var reused = provider.ResolvePort(new Point(112, 26), preview: false)!;
        Assert.Same(port, reused);

        // CancelResolve should be a no-op since we were reusing, not creating
        provider.CancelResolve();

        // Original port must still be there
        Assert.Single(provider.Ports);
        Assert.Same(port, provider.Ports[0]);
    }
}
