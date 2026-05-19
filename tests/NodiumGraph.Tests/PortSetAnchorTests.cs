using System;
using System.Collections.Generic;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortSetAnchorTests
{
    // Width/Height required so GetEdgePoint returns distinct values for different fractions.
    private static Node MakeNode() => new Node { Width = 100, Height = 100 };

    [Fact]
    public void AutoCtor_sets_IsAutoFraction_true_and_anchor_at_edge_half()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        Assert.True(port.IsAutoFraction);
        Assert.Equal(PortEdge.Left, port.Anchor.Edge);
        Assert.Equal(0.5, port.Anchor.Fraction);
    }

    [Fact]
    public void PinnedCtor_sets_IsAutoFraction_false()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.3));
        Assert.False(port.IsAutoFraction);
        Assert.Equal(0.3, port.Anchor.Fraction);
    }

    [Fact]
    public void SetAnchor_on_pinned_port_throws()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.3));
        Assert.Throws<InvalidOperationException>(() =>
            port.SetAnchor(new PortAnchor(PortEdge.Left, 0.7)));
    }

    [Fact]
    public void SetAnchor_different_edge_throws()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        Assert.Throws<InvalidOperationException>(() =>
            port.SetAnchor(new PortAnchor(PortEdge.Right, 0.5)));
    }

    [Fact]
    public void SetAnchor_same_anchor_fires_no_INPC()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        var fired = new List<string>();
        port.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        port.SetAnchor(new PortAnchor(PortEdge.Left, 0.5));   // same as ctor placeholder
        Assert.Empty(fired);
    }

    [Fact]
    public void SetAnchor_new_fraction_fires_INPC_for_derived_properties()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        var fired = new List<string>();
        port.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        port.SetAnchor(new PortAnchor(PortEdge.Left, 0.75));
        Assert.Contains(nameof(Port.Anchor), fired);
        Assert.Contains(nameof(Port.Position), fired);
        Assert.Contains(nameof(Port.AbsolutePosition), fired);
        Assert.Contains(nameof(Port.EmissionDirection), fired);
    }

    [Fact]
    public void SetAnchor_new_fraction_invalidates_position_cache()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        var before = port.Position;
        port.SetAnchor(new PortAnchor(PortEdge.Left, 0.9));
        var after = port.Position;
        Assert.NotEqual(before, after);
    }
}
