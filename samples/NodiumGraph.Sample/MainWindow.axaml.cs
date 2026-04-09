using Avalonia;
using Avalonia.Controls;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Sample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var graph = new Graph();

        // Node A: Start
        var nodeA = new Node { Title = "Start", X = 100, Y = 150 };
        var portAOut = new Port(nodeA, "Output", PortFlow.Output, new Point(140, 40));
        nodeA.PortProvider = new FixedPortProvider(new[] { portAOut });

        // Node B: Process
        var nodeB = new Node { Title = "Process", X = 400, Y = 100 };
        var portBIn = new Port(nodeB, "Input", PortFlow.Input, new Point(0, 40));
        var portBOut = new Port(nodeB, "Output", PortFlow.Output, new Point(140, 40));
        nodeB.PortProvider = new FixedPortProvider(new[] { portBIn, portBOut });

        // Node C: Filter
        var nodeC = new Node { Title = "Filter", X = 400, Y = 300 };
        var portCIn = new Port(nodeC, "Input", PortFlow.Input, new Point(0, 40));
        var portCOut = new Port(nodeC, "Output", PortFlow.Output, new Point(140, 40));
        nodeC.PortProvider = new FixedPortProvider(new[] { portCIn, portCOut });

        // Node D: End
        var nodeD = new Node { Title = "End", X = 700, Y = 200 };
        var portDIn = new Port(nodeD, "Input", PortFlow.Input, new Point(0, 40));
        nodeD.PortProvider = new FixedPortProvider(new[] { portDIn });

        // Comment
        var comment = new CommentNode { X = 100, Y = 50 };
        comment.Comment = "This is the pipeline entry point";

        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddNode(nodeC);
        graph.AddNode(nodeD);
        graph.AddNode(comment);

        graph.AddConnection(new Connection(portAOut, portBIn));
        graph.AddConnection(new Connection(portAOut, portCIn));
        graph.AddConnection(new Connection(portBOut, portDIn));

        Canvas.Graph = graph;

        // Wire a simple connection handler
        Canvas.ConnectionHandler = new SampleConnectionHandler(graph);
        Canvas.ConnectionValidator = new SampleConnectionValidator();
    }
}

file class SampleConnectionHandler(Graph graph) : IConnectionHandler
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

file class SampleConnectionValidator : IConnectionValidator
{
    public bool CanConnect(Port source, Port target)
    {
        if (source.Flow == target.Flow) return false;
        if (source.Owner == target.Owner) return false;
        return true;
    }
}
