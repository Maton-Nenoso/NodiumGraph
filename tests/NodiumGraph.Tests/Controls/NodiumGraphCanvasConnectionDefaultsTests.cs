using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
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
        var outPort = TestNodes.PortAt(nodeA, 100, 30, "Out", PortFlow.Output);
        outPort.DataType = "number";
        nodeA.PortProvider = new FixedPortProvider(new[] { outPort });
        graph.AddNode(nodeA);

        var nodeB = new Node { X = 200, Y = 0 };
        var inPort = TestNodes.PortAt(nodeB, 0, 30, "In", PortFlow.Input);
        inPort.DataType = "string";
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
        var outPort = TestNodes.PortAt(nodeA, 100, 30, "Out", PortFlow.Output);
        outPort.DataType = "number";
        nodeA.PortProvider = new FixedPortProvider(new[] { outPort });
        graph.AddNode(nodeA);

        var nodeB = new Node { X = 200, Y = 0 };
        var inPort = TestNodes.PortAt(nodeB, 0, 30, "In", PortFlow.Input);
        inPort.DataType = "number";
        nodeB.PortProvider = new FixedPortProvider(new[] { inPort });
        graph.AddNode(nodeB);

        canvas.Graph = graph;

        Assert.True(canvas.ConnectionValidator!.CanConnect(outPort, inPort));
    }
}
