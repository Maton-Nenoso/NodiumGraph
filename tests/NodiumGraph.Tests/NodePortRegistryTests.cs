using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class NodePortRegistryTests
{
    public NodePortRegistryTests() => NodePortRegistry.Clear();

    private class NodeA : Node { }
    private sealed class NodeB : Node { }
    private sealed class DerivedA : NodeA { }

    private static PortDefinition Def(string name, PortFlow flow = PortFlow.Input,
                                      PortEdge edge = PortEdge.Left, double fraction = 0.5,
                                      string? label = null, uint? maxConnections = null,
                                      object? dataType = null)
        => new() { Name = name, Flow = flow, Edge = edge, Fraction = fraction,
                   Label = label, MaxConnections = maxConnections, DataType = dataType };

    [Fact]
    public void TryGet_returns_registered_snapshot()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in"), Def("out", PortFlow.Output, PortEdge.Right) });
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("in",  snapshot[0].Name);
        Assert.Equal("out", snapshot[1].Name);
        Assert.Equal(PortFlow.Output, snapshot[1].Flow);
        Assert.Equal(PortEdge.Right,  snapshot[1].Edge);
    }

    [Fact]
    public void TryGet_unregistered_type_returns_false()
    {
        Assert.False(NodePortRegistry.TryGet(typeof(NodeB), out var snapshot));
        Assert.Empty(snapshot);
    }

    [Fact]
    public void TryGet_does_not_walk_to_base_type()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        Assert.False(NodePortRegistry.TryGet(typeof(DerivedA), out _));
    }

    [Fact]
    public void Register_identical_list_is_silent_no_op()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in"), Def("out", PortFlow.Output, PortEdge.Right) });
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in"), Def("out", PortFlow.Output, PortEdge.Right) });
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Equal(2, snapshot.Count);
    }

    [Fact]
    public void Register_different_list_throws()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("out", PortFlow.Output, PortEdge.Right) }));
        Assert.Contains("in",  ex.Message);
        Assert.Contains("out", ex.Message);
    }

    [Fact]
    public void Register_rejects_null_type()
        => Assert.Throws<ArgumentNullException>(() => NodePortRegistry.Register(null!, new[] { Def("in") }));

    [Fact]
    public void Register_rejects_non_node_type()
        => Assert.Throws<ArgumentException>(() => NodePortRegistry.Register(typeof(string), new[] { Def("in") }));

    [Fact]
    public void Register_rejects_empty_name()
        => Assert.Throws<ArgumentException>(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("") }));

    [Fact]
    public void Register_rejects_duplicate_names()
        => Assert.Throws<ArgumentException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x"), Def("x", PortFlow.Output) }));

    [Fact]
    public void Register_rejects_out_of_range_fraction()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", fraction: 1.5) }));

    [Fact]
    public void Register_rejects_undefined_edge()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", edge: (PortEdge)42) }));

    [Theory]
    [InlineData(null)]
    [InlineData("x")]
    [InlineData(42)]
    public void Register_accepts_allowed_DataType_values(object? dt)
        => NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: dt) });

    [Fact]
    public void Register_accepts_Type_as_DataType()
        => NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: typeof(int)) });

    [Fact]
    public void Register_accepts_enum_as_DataType()
        => NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: PortFlow.Input) });

    [Fact]
    public void Register_rejects_class_DataType()
        => Assert.Throws<ArgumentException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: new object()) }));

    [Fact]
    public void Snapshot_decoupled_from_source_PortDefinitions()
    {
        var def = Def("in");
        NodePortRegistry.Register(typeof(NodeA), new[] { def });
        def.Name = "mutated";
        NodePortRegistry.TryGet(typeof(NodeA), out var snapshot);
        Assert.Equal("in", snapshot[0].Name);
    }

    [Fact]
    public void Snapshot_decoupled_from_source_list_mutation()
    {
        var list = new List<PortDefinition> { Def("in") };
        NodePortRegistry.Register(typeof(NodeA), list);
        list.Clear();
        NodePortRegistry.TryGet(typeof(NodeA), out var snapshot);
        Assert.Single(snapshot);
    }

    [Fact]
    public void Snapshot_is_not_castable_to_writable_List()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        NodePortRegistry.TryGet(typeof(NodeA), out var snapshot);
        Assert.IsNotType<List<PortSpec>>(snapshot);
    }

    [Fact]
    public void Clear_empties_the_registry()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        NodePortRegistry.Clear();
        Assert.False(NodePortRegistry.TryGet(typeof(NodeA), out _));
    }

    [Fact]
    public async Task Concurrent_register_with_different_defs_throws_on_one_thread()
    {
        var t1 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("a") }));
        var t2 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("b") }));
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(t1, t2));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Single(snapshot);
    }

    [Fact]
    public async Task Concurrent_register_with_identical_defs_both_succeed()
    {
        var t1 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("a") }));
        var t2 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("a") }));
        await Task.WhenAll(t1, t2);
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Single(snapshot);
    }
}

[CollectionDefinition("NodePortRegistry", DisableParallelization = true)]
public class NodePortRegistryCollection { }
