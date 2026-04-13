using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests.Controls;

public class NodiumGraphCanvasConnectionDefaultsTests
{
    [AvaloniaFact]
    public void Default_validator_rejects_mismatched_datatypes()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();

        var nodeA = new Node { X = 0, Y = 0 };
        var outPort = new Port(nodeA, "Out", PortFlow.Output, new Point(100, 30))
        {
            DataType = "number",
        };
        nodeA.PortProvider = new FixedPortProvider(new[] { outPort });
        graph.AddNode(nodeA);

        var nodeB = new Node { X = 200, Y = 0 };
        var inPort = new Port(nodeB, "In", PortFlow.Input, new Point(0, 30))
        {
            DataType = "string",
        };
        nodeB.PortProvider = new FixedPortProvider(new[] { inPort });
        graph.AddNode(nodeB);

        canvas.Graph = graph;

        Assert.False(canvas.ConnectionValidator!.CanConnect(outPort, inPort));
    }

    [AvaloniaFact]
    public void Default_validator_accepts_matching_datatypes()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();

        var nodeA = new Node { X = 0, Y = 0 };
        var outPort = new Port(nodeA, "Out", PortFlow.Output, new Point(100, 30))
        {
            DataType = "number",
        };
        nodeA.PortProvider = new FixedPortProvider(new[] { outPort });
        graph.AddNode(nodeA);

        var nodeB = new Node { X = 200, Y = 0 };
        var inPort = new Port(nodeB, "In", PortFlow.Input, new Point(0, 30))
        {
            DataType = "number",
        };
        nodeB.PortProvider = new FixedPortProvider(new[] { inPort });
        graph.AddNode(nodeB);

        canvas.Graph = graph;

        Assert.True(canvas.ConnectionValidator!.CanConnect(outPort, inPort));
    }
}
