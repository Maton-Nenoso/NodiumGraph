using System;
using System.Reflection;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class CanvasMaterializationTests
{
    public CanvasMaterializationTests() => NodePortRegistry.Clear();

    private sealed class CanvasNodeA : Node { }

    [AvaloniaFact]
    public void Adding_node_with_registry_entry_attaches_provider_exactly_once()
    {
        NodePortRegistry.Register(typeof(CanvasNodeA), new[]
        {
            new PortDefinition { Name = "in",  Flow = PortFlow.Input,  Edge = PortEdge.Left,  Fraction = 0.5 },
            new PortDefinition { Name = "out", Flow = PortFlow.Output, Edge = PortEdge.Right, Fraction = 0.5 },
        });

        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;

        var node = new CanvasNodeA { Width = 100, Height = 50 };
        graph.AddNode(node);

        var provider = node.PortProvider!;

        // Public `event Action<Port>? PortAdded;` compiles to a private backing field
        // with the same name. Inspect it to count subscribers.
        var backing = typeof(FixedPortProvider).GetField("PortAdded",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(backing);
        var del = (Action<Port>?)backing!.GetValue(provider);
        var subscriberCount = del?.GetInvocationList().Length ?? 0;

        Assert.Equal(1, subscriberCount);
    }
}
