using Avalonia;
using Avalonia.Controls;
using NodiumGraph;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace GettingStarted;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var graph = BuildGraph();
        Canvas.Graph = graph;
        Canvas.ConnectionHandler = new GraphConnectionHandler(graph);
    }

    private static Graph BuildGraph()
    {
        var graph = new Graph();

        var source = CreateMathNode("Source", "Produces a number", x: 120, y: 200);
        var sink = CreateMathNode("Sink", "Consumes a number", x: 480, y: 200);

        graph.AddNode(source);
        graph.AddNode(sink);

        var sourceOut = source.PortProvider!.Ports[1];
        var sinkIn = sink.PortProvider!.Ports[0];
        graph.AddConnection(new Connection(sourceOut, sinkIn));

        return graph;
    }

    private static MathNode CreateMathNode(string title, string description, double x, double y)
    {
        var node = new MathNode
        {
            Title = title,
            Description = description,
            X = x,
            Y = y,
        };

        var provider = new FixedPortProvider(layoutAware: true);
        provider.AddPort(new Port(node, "in", PortFlow.Input, new Point(0, 40))
        {
            Label = "in",
            DataType = "number",
        });
        provider.AddPort(new Port(node, "out", PortFlow.Output, new Point(180, 40))
        {
            Label = "out",
            DataType = "number",
        });

        node.PortProvider = provider;
        return node;
    }
}

file sealed class GraphConnectionHandler(Graph graph) : IConnectionHandler
{
    public Result<Connection> OnConnectionRequested(Port source, Port target)
    {
        var connection = new Connection(source, target);
        graph.AddConnection(connection);
        return connection;
    }

    public void OnConnectionDeleteRequested(Connection connection)
    {
        graph.RemoveConnection(connection);
    }
}
