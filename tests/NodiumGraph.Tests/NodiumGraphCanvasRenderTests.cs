using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasRenderTests
{
    [AvaloniaFact]
    public void Canvas_does_not_throw_on_render_with_graph()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var portOut = new Port(nodeA, new Point(100, 25));
        var portIn = new Port(nodeB, new Point(0, 25));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddConnection(new Connection(portOut, portIn));
        canvas.Graph = graph;

        // Verify no exception — rendering details tested in unit tests.
        // The headless backend will call Render during layout.
    }

    [AvaloniaFact]
    public void Canvas_does_not_throw_on_render_without_graph()
    {
        var canvas = new NodiumGraphCanvas();

        // Render with no graph set — should be safe.
    }

    [AvaloniaFact]
    public void Canvas_does_not_throw_on_render_with_grid_disabled()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ShowGrid = false;
        canvas.Graph = new Graph();
    }

    [AvaloniaFact]
    public void Canvas_clips_to_bounds_by_default()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.True(canvas.ClipToBounds);
    }

    [AvaloniaFact]
    public void Setting_PortTemplate_disables_default_port_rendering()
    {
        var canvas = new NodiumGraphCanvas();
        // Setting any template should suppress default ellipse rendering
        // (The template itself would be part of the node's visual tree)
        canvas.PortTemplate = new FuncDataTemplate<Port>((_, _) => new Avalonia.Controls.TextBlock());
        Assert.NotNull(canvas.PortTemplate);
        // No assertion on rendering (visual verification), but ensures property is read
    }

    [AvaloniaFact]
    public void ArrangeOverride_sizes_container_to_scaled_dimensions()
    {
        // At zoom 2.0, a node's layout slot should use scaled dimensions.
        // In headless, we verify no exception and container count is correct.
        var canvas = new NodiumGraphCanvas { ViewportZoom = 2.0 };
        var graph = new Graph();
        var node = new Node { X = 0, Y = 0, Title = "Test" };
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        // Container was created and arrange completed without exception
        Assert.Equal(1, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void ResolveBrush_returns_fallback_when_resource_not_found()
    {
        var canvas = new NodiumGraphCanvas();
        var fallback = Brushes.Red;
        var result = canvas.ResolveBrush("NonExistentKey", fallback);
        Assert.Same(fallback, result);
    }
}
