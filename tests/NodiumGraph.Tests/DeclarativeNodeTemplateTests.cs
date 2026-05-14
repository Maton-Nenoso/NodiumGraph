using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class DeclarativeNodeTemplateTests
{
    public DeclarativeNodeTemplateTests() => NodePortRegistry.Clear();

    [AvaloniaFact]
    public void Window_load_populates_registry_for_NodeTemplate_with_Ports()
    {
        _ = new DeclarativePortsTestWindow();

        Assert.True(NodePortRegistry.TryGet(typeof(DeclarativeNodeA), out var specs));
        Assert.Equal(2, specs.Count);
        Assert.Equal("in",     specs[0].Name);
        Assert.Equal("out",    specs[1].Name);
        Assert.Equal("result", specs[1].Label);
    }

    [AvaloniaFact]
    public void Window_load_does_not_register_visual_only_NodeTemplate()
    {
        _ = new DeclarativePortsTestWindow();

        Assert.False(NodePortRegistry.TryGet(typeof(DeclarativeNodeVisualOnly), out _));
    }

    [AvaloniaFact]
    public void Node_constructed_after_window_load_has_materialized_ports()
    {
        _ = new DeclarativePortsTestWindow();

        var node = new DeclarativeNodeA { Width = 100, Height = 50 };

        Assert.Equal(2, node.Ports.Count);
        Assert.NotNull(node.PortProvider);
    }

    [AvaloniaFact]
    public void Derived_type_with_no_own_registration_does_not_inherit_base_template_ports()
    {
        _ = new DeclarativePortsTestWindow();

        var node = new DeclarativeNodeDerivedA { Width = 100, Height = 50 };

        Assert.Empty(node.Ports);
        Assert.Null(node.PortProvider);
    }

    [AvaloniaFact]
    public void NodeTemplate_Match_is_exact_type_only()
    {
        var template = new NodeTemplate { DataType = typeof(DeclarativeNodeA) };

        Assert.True(template.Match(new DeclarativeNodeA()));
        Assert.False(template.Match(new DeclarativeNodeDerivedA()));
    }

    [AvaloniaFact]
    public void Canvas_render_path_attaches_materialized_provider_via_graph_binding()
    {
        // Show() is intentionally omitted: it triggers a render pass that, when interleaved
        // with other parallel Avalonia tests, leaks brush ownership across dispatcher threads
        // and fails in cleanup. The graph-binding contract under test (provider materialization
        // on AddNode) doesn't require a render pass.
        var window = new DeclarativePortsTestWindow();
        var canvas = window.FindControl<NodiumGraphCanvas>("Canvas")!;

        var graph = new Graph();
        canvas.Graph = graph;

        var node = new DeclarativeNodeA { Width = 100, Height = 50 };
        graph.AddNode(node);

        Assert.NotNull(node.PortProvider);
        Assert.Equal(2, node.PortProvider!.Ports.Count);
    }
}
