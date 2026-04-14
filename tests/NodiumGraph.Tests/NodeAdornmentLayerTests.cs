using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeAdornmentLayerTests
{
    [AvaloniaFact]
    public void NodiumNodeContainer_has_content_presenter_and_adornment_layer_in_order()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        var node = new Node();
        canvas.Graph!.AddNode(node);

        var container = canvas.GetInternalNodeContainer(node);
        Assert.NotNull(container);
        Assert.Equal(2, container!.Children.Count);
        Assert.IsType<ContentPresenter>(container.Children[0]);
        Assert.IsType<NodeAdornmentLayer>(container.Children[1]);
    }

    [AvaloniaFact]
    public void NodeAdornmentLayer_is_not_hit_test_visible()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        var node = new Node();
        canvas.Graph!.AddNode(node);

        var container = canvas.GetInternalNodeContainer(node)!;
        var adornments = container.Children.OfType<NodeAdornmentLayer>().Single();

        Assert.False(adornments.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void Adornment_layer_is_the_last_visual_child_of_its_container()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        var node = new Node();
        canvas.Graph!.AddNode(node);

        var container = canvas.GetInternalNodeContainer(node)!;

        // Z-order pin: adornments must render AFTER the content so per-node
        // decorations (borders, ports, labels) draw over the node's own body
        // but under any sibling node's body. Last child in a Panel is the
        // highest-z visual child.
        Assert.IsType<NodeAdornmentLayer>(container.Children[^1]);
    }
}
