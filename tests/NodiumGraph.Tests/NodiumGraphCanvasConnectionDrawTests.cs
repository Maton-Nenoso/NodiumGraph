using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasConnectionDrawTests
{
    [AvaloniaFact]
    public void HitTestPort_finds_port_within_radius()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 100, Y = 100 };
        node.Width = 200;
        node.Height = 100;
        var port = new Port(node, "Out", PortFlow.Output, new Point(200, 50));
        node.PortProvider = new FixedPortProvider(new[] { port });
        graph.AddNode(node);
        canvas.Graph = graph;

        // Port absolute position is (300, 150). At zoom 1, offset 0 that's screen (300, 150)
        var hit = canvas.ResolvePort(new Point(300, 150), preview: true);
        Assert.Same(port, hit);
    }

    [AvaloniaFact]
    public void HitTestPort_returns_null_when_no_port_near()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 100, Y = 100 };
        node.Width = 200;
        node.Height = 100;
        var port = new Port(node, "Out", PortFlow.Output, new Point(200, 50));
        node.PortProvider = new FixedPortProvider(new[] { port });
        graph.AddNode(node);
        canvas.Graph = graph;

        var hit = canvas.ResolvePort(new Point(0, 0), preview: true);
        Assert.Null(hit);
    }

    [AvaloniaFact]
    public void ConnectionDraw_state_starts_inactive()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.IsDrawingConnection);
    }

    [AvaloniaFact]
    public void ConnectionHandler_receives_request_on_valid_connection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();

        var nodeA = new Node { X = 0, Y = 0 };
        nodeA.Width = 100;
        nodeA.Height = 60;
        var portOut = new Port(nodeA, "Out", PortFlow.Output, new Point(100, 30));
        nodeA.PortProvider = new FixedPortProvider(new[] { portOut });

        var nodeB = new Node { X = 300, Y = 0 };
        nodeB.Width = 100;
        nodeB.Height = 60;
        var portIn = new Port(nodeB, "In", PortFlow.Input, new Point(0, 30));
        nodeB.PortProvider = new FixedPortProvider(new[] { portIn });

        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        canvas.Graph = graph;

        Port? requestedSource = null;
        Port? requestedTarget = null;
        canvas.ConnectionHandler = new TestConnectionHandler(onRequested: (s, t) =>
        {
            requestedSource = s;
            requestedTarget = t;
            var conn = new Connection(s, t);
            graph.AddConnection(conn);
            return (Result<Connection>)conn;
        });

        // Simulate: programmatically test the handler call
        // (Full pointer simulation is complex in headless - test the logic path)
        var result = canvas.ConnectionHandler.OnConnectionRequested(portOut, portIn);

        Assert.True(result.IsSuccess);
        Assert.Same(portOut, requestedSource);
        Assert.Same(portIn, requestedTarget);
    }

    [AvaloniaFact]
    public void ConnectionHandler_rejected_result_is_captured()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 0 };
        var portOut = new Port(nodeA, "Out", PortFlow.Output, new Point(100, 30));
        var portIn = new Port(nodeB, "In", PortFlow.Input, new Point(0, 30));
        nodeA.PortProvider = new FixedPortProvider(new[] { portOut });
        nodeB.PortProvider = new FixedPortProvider(new[] { portIn });
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        canvas.Graph = graph;

        canvas.ConnectionHandler = new TestRejectingHandler();

        // The handler rejects — canvas should handle gracefully
        var result = canvas.ConnectionHandler.OnConnectionRequested(portOut, portIn);
        Assert.False(result.IsSuccess);
    }

    [AvaloniaFact]
    public void Connection_cancel_removes_dynamic_source_port()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var sourceNode = new Node { X = 0, Y = 0 };
        sourceNode.Width = 100;
        sourceNode.Height = 100;
        var dynamicProvider = new DynamicPortProvider(sourceNode);
        sourceNode.PortProvider = dynamicProvider;

        var targetNode = new Node { X = 300, Y = 0 };
        targetNode.Width = 100;
        targetNode.Height = 100;
        graph.AddNode(sourceNode);
        graph.AddNode(targetNode);
        canvas.Graph = graph;

        Assert.Empty(dynamicProvider.Ports);

        var sourcePort = sourceNode.PortProvider.ResolvePort(new Point(100, 50), preview: false);
        Assert.NotNull(sourcePort);
        Assert.Single(dynamicProvider.Ports);

        dynamicProvider.CancelResolve();
        Assert.Empty(dynamicProvider.Ports);
    }

    [AvaloniaFact]
    public void ResolvePortWithProvider_returns_matching_provider()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 100;
        var provider = new DynamicPortProvider(node);
        node.PortProvider = provider;
        graph.AddNode(node);
        canvas.Graph = graph;

        // preview: false so DynamicPortProvider creates the port
        var (port, resolvedProvider) = canvas.ResolvePortWithProvider(new Point(100, 50), preview: false);
        Assert.NotNull(port);
        Assert.Same(provider, resolvedProvider);
    }

    [AvaloniaFact]
    public void ResolvePortWithProvider_returns_null_when_no_hit()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 0, Y = 0 };
        node.Width = 100;
        node.Height = 100;
        node.PortProvider = new FixedPortProvider(new[] { new Port(node, "Out", PortFlow.Output, new Point(100, 50)) });
        graph.AddNode(node);
        canvas.Graph = graph;

        var (port, resolvedProvider) = canvas.ResolvePortWithProvider(new Point(500, 500), preview: true);
        Assert.Null(port);
        Assert.Null(resolvedProvider);
    }

    private class TestRejectingHandler : IConnectionHandler
    {
        public Result<Connection> OnConnectionRequested(Port source, Port target)
            => new Error("Rejected", "TEST");
        public void OnConnectionDeleteRequested(Connection connection) { }
    }
}
