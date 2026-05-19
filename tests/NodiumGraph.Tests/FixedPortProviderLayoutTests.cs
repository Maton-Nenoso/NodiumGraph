using System.Collections.Generic;
using System.Linq;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class FixedPortProviderLayoutTests
{
    private static Node MakeNode() => new Node();

    private static Port[] AutoPortsOnLeft(Node node, int count)
    {
        var arr = new Port[count];
        for (int i = 0; i < count; i++)
            arr[i] = new Port(node, $"In{i + 1}", PortFlow.Input, PortEdge.Left);
        return arr;
    }

    [Fact]
    public void Ctor_three_auto_on_left_distributes()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        _ = new FixedPortProvider(ports);
        Assert.Equal(0.25, ports[0].Anchor.Fraction);
        Assert.Equal(0.50, ports[1].Anchor.Fraction);
        Assert.Equal(0.75, ports[2].Anchor.Fraction);
    }

    [Fact]
    public void Ctor_five_auto_on_left_distributes()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 5);
        _ = new FixedPortProvider(ports);
        for (int i = 0; i < 5; i++)
            Assert.Equal((i + 1.0) / 6.0, ports[i].Anchor.Fraction, 9);
    }

    [Fact]
    public void Ctor_single_auto_on_left_at_half()
    {
        var node = MakeNode();
        var port = new Port(node, "Only", PortFlow.Input, PortEdge.Left);
        _ = new FixedPortProvider(new[] { port });
        Assert.Equal(0.5, port.Anchor.Fraction);
    }

    [Fact]
    public void Ctor_pinned_and_auto_on_same_edge_auto_ignores_pinned()
    {
        var node = MakeNode();
        var pinnedHi = new Port(node, "Hi", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.1));
        var auto1 = new Port(node, "A1", PortFlow.Input, PortEdge.Left);
        var auto2 = new Port(node, "A2", PortFlow.Input, PortEdge.Left);
        var pinnedLo = new Port(node, "Lo", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.9));
        _ = new FixedPortProvider(new[] { pinnedHi, auto1, auto2, pinnedLo });
        Assert.Equal(0.1, pinnedHi.Anchor.Fraction);
        Assert.Equal(0.9, pinnedLo.Anchor.Fraction);
        Assert.Equal(1.0 / 3.0, auto1.Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, auto2.Anchor.Fraction, 9);
    }

    [Fact]
    public void Ctor_all_pinned_fires_no_port_level_INPC()
    {
        var node = MakeNode();
        var p1 = new Port(node, "P1", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.2));
        var p2 = new Port(node, "P2", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.7));
        var fired = new List<string>();
        p1.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        p2.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        _ = new FixedPortProvider(new[] { p1, p2 });
        Assert.Empty(fired);
    }

    [Fact]
    public void Ctor_does_not_fire_PortAdded_for_initial_ports()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 2);
        var provider = new FixedPortProvider(ports);
        var added = new List<Port>();
        provider.PortAdded += p => added.Add(p);
        Assert.Empty(added);
    }

    [Fact]
    public void AddPort_auto_on_existing_edge_triggers_relayout()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        var provider = new FixedPortProvider(ports);
        var p4 = new Port(node, "In4", PortFlow.Input, PortEdge.Left);
        provider.AddPort(p4);
        Assert.Equal(0.2, ports[0].Anchor.Fraction, 9);
        Assert.Equal(0.4, ports[1].Anchor.Fraction, 9);
        Assert.Equal(0.6, ports[2].Anchor.Fraction, 9);
        Assert.Equal(0.8, p4.Anchor.Fraction, 9);
    }

    [Fact]
    public void AddPort_pinned_does_not_relayout_auto()
    {
        var node = MakeNode();
        var auto1 = new Port(node, "A1", PortFlow.Input, PortEdge.Left);
        var auto2 = new Port(node, "A2", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { auto1, auto2 });
        var pinned = new Port(node, "P", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.5));
        provider.AddPort(pinned);
        Assert.Equal(1.0 / 3.0, auto1.Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, auto2.Anchor.Fraction, 9);
    }

    [Fact]
    public void AddPort_layout_runs_before_PortAdded_fires()
    {
        var node = MakeNode();
        var existing = new Port(node, "E", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { existing });
        double observedExistingFraction = -1;
        provider.PortAdded += _ => observedExistingFraction = existing.Anchor.Fraction;
        var added = new Port(node, "A", PortFlow.Input, PortEdge.Left);
        provider.AddPort(added);
        Assert.Equal(1.0 / 3.0, observedExistingFraction, 9);
    }

    [Fact]
    public void RemovePort_auto_mid_triggers_relayout()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        var provider = new FixedPortProvider(ports);
        provider.RemovePort(ports[1]);
        Assert.Equal(1.0 / 3.0, ports[0].Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, ports[2].Anchor.Fraction, 9);
    }

    [Fact]
    public void RemovePort_last_auto_on_edge_no_op()
    {
        var node = MakeNode();
        var port = new Port(node, "Only", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { port });
        var removed = provider.RemovePort(port);
        Assert.True(removed);
    }

    [Fact]
    public void RemovePort_pinned_does_not_relayout_auto()
    {
        var node = MakeNode();
        var pinned = new Port(node, "P", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.5));
        var auto1 = new Port(node, "A1", PortFlow.Input, PortEdge.Left);
        var auto2 = new Port(node, "A2", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { pinned, auto1, auto2 });
        provider.RemovePort(pinned);
        Assert.Equal(1.0 / 3.0, auto1.Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, auto2.Anchor.Fraction, 9);
    }

    [Fact]
    public void RemovePort_layout_runs_before_PortRemoved_fires()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        var provider = new FixedPortProvider(ports);
        double observedFirstFraction = -1;
        provider.PortRemoved += _ => observedFirstFraction = ports[0].Anchor.Fraction;
        provider.RemovePort(ports[1]);
        Assert.Equal(1.0 / 3.0, observedFirstFraction, 9);
    }

    [Fact]
    public void Auto_ports_preserve_edge_across_layout()
    {
        var node = MakeNode();
        var leftA = new Port(node, "LA", PortFlow.Input, PortEdge.Left);
        var rightA = new Port(node, "RA", PortFlow.Output, PortEdge.Right);
        var provider = new FixedPortProvider(new[] { leftA, rightA });
        provider.AddPort(new Port(node, "LB", PortFlow.Input, PortEdge.Left));
        Assert.Equal(PortEdge.Left, leftA.Anchor.Edge);
        Assert.Equal(PortEdge.Right, rightA.Anchor.Edge);
    }
}
