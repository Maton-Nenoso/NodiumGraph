using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class NodeRegistryMaterializationTests
{
    public NodeRegistryMaterializationTests() => NodePortRegistry.Clear();

    private sealed class TypeA : Node { }
    private sealed class TypeB : Node { }

    private static PortDefinition Def(string name, PortFlow flow = PortFlow.Input,
                                      PortEdge edge = PortEdge.Left, double fraction = 0.5,
                                      string? label = null, uint? maxConnections = null, object? dataType = null)
        => new() { Name = name, Flow = flow, Edge = edge, Fraction = fraction,
                   Label = label, MaxConnections = maxConnections, DataType = dataType };

    [Fact]
    public void Ports_materializes_from_registry()
    {
        NodePortRegistry.Register(typeof(TypeA), new[]
        {
            Def("in",  PortFlow.Input,  PortEdge.Left,  0.5),
            Def("out", PortFlow.Output, PortEdge.Right, 0.5),
        });
        var node = new TypeA { Width = 100, Height = 50 };

        var ports = node.Ports;

        Assert.Equal(2, ports.Count);
        Assert.Equal("in",  ports[0].Name);
        Assert.Equal("out", ports[1].Name);
        Assert.IsType<FixedPortProvider>(node.PortProvider);
    }

    [Fact]
    public void PortProvider_getter_also_triggers_materialization()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };

        var provider = node.PortProvider;

        Assert.NotNull(provider);
        Assert.Single(provider!.Ports);
    }

    [Fact]
    public void Pre_assigned_provider_wins_over_registry()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        var custom = new FixedPortProvider();
        node.PortProvider = custom;

        Assert.Same(custom, node.PortProvider);
        Assert.Empty(node.Ports);
    }

    [Fact]
    public void Explicit_null_suppresses_registry_permanently()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        node.PortProvider = null;

        Assert.Null(node.PortProvider);
        Assert.Empty(node.Ports);
    }

    [Fact]
    public void Unregistered_type_stays_portless()
    {
        var node = new TypeB { Width = 100, Height = 50 };
        Assert.Empty(node.Ports);
        Assert.Null(node.PortProvider);
    }

    [Fact]
    public void Late_registration_is_picked_up_on_next_access()
    {
        var node = new TypeA { Width = 100, Height = 50 };
        Assert.Empty(node.Ports);

        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });

        Assert.Single(node.Ports);
        Assert.NotNull(node.PortProvider);
    }

    [Fact]
    public void Optional_fields_propagate_to_materialized_Port()
    {
        NodePortRegistry.Register(typeof(TypeA), new[]
        {
            Def("x", label: "Label", maxConnections: 3u, dataType: "number"),
        });
        var node = new TypeA { Width = 100, Height = 50 };

        var port = node.Ports.Single();

        Assert.Equal("Label",  port.Label);
        Assert.Equal(3u,       port.MaxConnections);
        Assert.Equal("number", port.DataType);
    }

    [Fact]
    public void Repeated_Ports_access_does_not_re_materialize()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };

        var first  = node.Ports;
        var second = node.Ports;

        Assert.Same(first, second);
    }

    [Fact]
    public void Materialization_fires_PropertyChanged_once()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        var fires = new List<string?>();
        node.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Node.PortProvider)) fires.Add(e.PropertyName); };

        _ = node.Ports;
        _ = node.Ports;
        _ = node.PortProvider;

        Assert.Single(fires);
    }

    [Fact]
    public void Clear_does_not_affect_already_materialized_node()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        var providerBefore = node.PortProvider;

        NodePortRegistry.Clear();

        Assert.Same(providerBefore, node.PortProvider);
    }

    [Fact]
    public void EnsureMaterialized_three_auto_specs_produce_evenly_distributed_ports()
    {
        var defs = new[]
        {
            new PortDefinition { Name = "In1", Flow = PortFlow.Input, Edge = PortEdge.Left },
            new PortDefinition { Name = "In2", Flow = PortFlow.Input, Edge = PortEdge.Left },
            new PortDefinition { Name = "In3", Flow = PortFlow.Input, Edge = PortEdge.Left },
        };
        NodePortRegistry.Register(typeof(TypeA), defs);
        var node = new TypeA { Width = 100, Height = 50 };

        var ports = node.Ports.ToList();

        Assert.Equal(3, ports.Count);
        Assert.Equal(0.25, ports[0].Anchor.Fraction);
        Assert.Equal(0.50, ports[1].Anchor.Fraction);
        Assert.Equal(0.75, ports[2].Anchor.Fraction);
    }

    [Fact]
    public void EnsureMaterialized_mixed_spec_respects_pinned_and_distributes_auto()
    {
        var defs = new[]
        {
            new PortDefinition { Name = "Top",    Flow = PortFlow.Input, Edge = PortEdge.Left, Fraction = 0.1 },
            new PortDefinition { Name = "Mid1",   Flow = PortFlow.Input, Edge = PortEdge.Left },
            new PortDefinition { Name = "Mid2",   Flow = PortFlow.Input, Edge = PortEdge.Left },
            new PortDefinition { Name = "Bottom", Flow = PortFlow.Input, Edge = PortEdge.Left, Fraction = 0.9 },
        };
        NodePortRegistry.Register(typeof(TypeB), defs);
        var node = new TypeB { Width = 100, Height = 50 };

        var byName = node.Ports.ToDictionary(p => p.Name);

        Assert.Equal(0.1, byName["Top"].Anchor.Fraction);
        Assert.Equal(0.9, byName["Bottom"].Anchor.Fraction);
        Assert.Equal(1.0 / 3.0, byName["Mid1"].Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, byName["Mid2"].Anchor.Fraction, 9);
    }

    [Fact]
    public void EnsureMaterialized_auto_spec_port_has_IsAutoFraction_true()
    {
        var defs = new[]
        {
            new PortDefinition { Name = "Auto",   Flow = PortFlow.Input,  Edge = PortEdge.Left },
            new PortDefinition { Name = "Pinned", Flow = PortFlow.Input,  Edge = PortEdge.Right, Fraction = 0.5 },
        };
        NodePortRegistry.Register(typeof(TypeA), defs);
        var node = new TypeA { Width = 100, Height = 50 };

        var byName = node.Ports.ToDictionary(p => p.Name);

        Assert.True(byName["Auto"].IsAutoFraction);
        Assert.False(byName["Pinned"].IsAutoFraction);
    }
}
