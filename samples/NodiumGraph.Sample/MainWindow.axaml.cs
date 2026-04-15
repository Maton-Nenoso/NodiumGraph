using System.Collections.Generic;
using System.Linq;
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

        // -- 1. Input Source --
        // Rectangle shape, 1 output port on the right side
        var inputNode = new InputSourceNode
        {
            Title = "Input Source",
            X = 100,
            Y = 200,
        };
        var inputProvider = new FixedPortProvider(layoutAware: true);
        var inputOut = new Port(inputNode, "out", PortFlow.Output, new Point(120, 30)) { Label = "out", DataType = "number" };
        inputProvider.AddPort(inputOut);
        inputNode.PortProvider = inputProvider;

        // -- 2. Transform --
        // RoundedRectangleShape(8), 1 input on left, 2 outputs on right side,
        // blue header with custom border, diamond-shaped output ports
        var transformNode = new TransformNode
        {
            Title = "Transform",
            X = 350,
            Y = 150,
            IsCollapsible = true,
            Shape = new RoundedRectangleShape(8),
        };
        var diamondStyle = new PortStyle { Shape = PortShape.Diamond };
        var transformProvider = new FixedPortProvider(layoutAware: true);
        var transformIn = new Port(transformNode, "in", PortFlow.Input, new Point(0, 30)) { Label = "in", DataType = "number" };
        var transformOut1 = new Port(transformNode, "out1", PortFlow.Output, new Point(120, 15)) { Label = "out1", Style = diamondStyle, DataType = "number" };
        var transformOut2 = new Port(transformNode, "out2", PortFlow.Output, new Point(120, 45)) { Label = "out2", Style = diamondStyle, DataType = "number" };
        transformProvider.AddPort(transformIn);
        transformProvider.AddPort(transformOut1);
        transformProvider.AddPort(transformOut2);
        transformNode.PortProvider = transformProvider;

        // -- 3. Filter --
        // EllipseShape, 1 input on left, 1 output on right, orange header with opacity
        var filterNode = new FilterNode
        {
            Title = "Filter",
            X = 350,
            Y = 350,
            IsCollapsible = true,
            Shape = new EllipseShape(),
        };
        var filterProvider = new FixedPortProvider(layoutAware: true);
        // DataType intentionally differs from upstream — default validator will reject the drag
        var filterIn = new Port(filterNode, "in", PortFlow.Input, new Point(0, 30)) { Label = "in", DataType = "string" };
        var filterOut = new Port(filterNode, "out", PortFlow.Output, new Point(120, 30)) { Label = "out", DataType = "string" };
        filterProvider.AddPort(filterIn);
        filterProvider.AddPort(filterOut);
        filterNode.PortProvider = filterProvider;

        // -- 4. Merge --
        // Rectangle shape, 2 inputs on the left (upper and lower), 1 output on the right,
        // ShowHeader = false, purple body
        var mergeNode = new MergeNode
        {
            Title = "Merge",
            X = 600,
            Y = 250,
            ShowHeader = false,
        };
        var mergeProvider = new FixedPortProvider(layoutAware: true);
        var mergeIn1 = new Port(mergeNode, "input1", PortFlow.Input, new Point(0, 15)) { Label = "input1", DataType = "number" };
        var mergeIn2 = new Port(mergeNode, "input2", PortFlow.Input, new Point(0, 45)) { Label = "input2", DataType = "number" };
        var mergeOut = new Port(mergeNode, "out", PortFlow.Output, new Point(120, 30)) { Label = "out", DataType = "number" };
        mergeProvider.AddPort(mergeIn1);
        mergeProvider.AddPort(mergeIn2);
        mergeProvider.AddPort(mergeOut);
        mergeNode.PortProvider = mergeProvider;

        // -- 5. Output Sink --
        // Rectangle shape, FixedPortProvider with 1 input on left, red header
        var outputNode = new OutputSinkNode
        {
            Title = "Output Sink",
            X = 850,
            Y = 250,
            IsCollapsible = true,
        };
        var outputIn = new Port(outputNode, "in", PortFlow.Input, new Point(0, 15)) { Label = "in", DataType = "number" };
        outputNode.PortProvider = new FixedPortProvider(new[] { outputIn });

        // -- 6. Comment --
        var comment = new CommentNode { X = 100, Y = 50 };
        comment.Comment = "This is the pipeline entry point";

        // -- Add nodes to graph --
        graph.AddNode(inputNode);
        graph.AddNode(transformNode);
        graph.AddNode(filterNode);
        graph.AddNode(mergeNode);
        graph.AddNode(outputNode);
        graph.AddNode(comment);

        // -- Connections --
        // Input -> Transform (out -> in)
        graph.AddConnection(new Connection(inputOut, transformIn));
        // Transform -> Merge (out1 -> input1)
        graph.AddConnection(new Connection(transformOut1, mergeIn1));
        // Transform -> Merge (out2 -> input2)
        graph.AddConnection(new Connection(transformOut2, mergeIn2));
        // Merge -> Output (out -> in)
        graph.AddConnection(new Connection(mergeOut, outputIn));
        // Filter is intentionally left unconnected — its "string" DataType
        // demonstrates the default validator rejecting type-mismatched drags
        // from the "number" transform outputs.

        // Don't set Canvas.NodeTemplate — let DataTemplate resolution from Window work
        Canvas.Graph = graph;

        // Decorate connections with an arrow at the target end — the common
        // directed-graph look. Default arrow size of 8 reads well at 1:1 zoom.
        Canvas.DefaultConnectionStyle = new ConnectionStyle(targetEndpoint: new ArrowEndpoint());

        // Wire a simple connection handler. The canvas uses DefaultConnectionValidator
        // out of the box, which enforces self/same-owner/Flow/DataType rules.
        Canvas.ConnectionHandler = new SampleConnectionHandler(graph);

        // Unified delete handler — fires on Delete key with a mixed selection of
        // nodes and connections. Library reports; consumer mutates the graph.
        Canvas.GraphInteractionHandler = new SampleGraphInteractionHandler(graph);

        // Wire grid style combo to canvas property
        GridStyleCombo.SelectionChanged += (_, _) =>
        {
            if (GridStyleCombo.SelectedItem is ComboBoxItem { Tag: GridStyle style })
                Canvas.GridStyle = style;
        };

        // Wire node feature toggles
        CollapseTransformToggle.IsCheckedChanged += (_, _) =>
        {
            transformNode.IsCollapsible = CollapseTransformToggle.IsChecked == true;
        };

        ShowMergeHeaderToggle.IsCheckedChanged += (_, _) =>
        {
            mergeNode.ShowHeader = ShowMergeHeaderToggle.IsChecked == true;
        };
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

file sealed class SampleGraphInteractionHandler(Graph graph) : IGraphInteractionHandler
{
    public void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)
    {
        // Remove connections first so node-cascade doesn't double-remove.
        // The library defensively snapshots before firing, so mutating the
        // graph from inside this callback is safe. The .ToList() calls here
        // are an extra belt-and-braces guard against reentrancy via
        // SelectedItems.CollectionChanged.
        foreach (var connection in elements.OfType<Connection>().ToList())
            graph.RemoveConnection(connection);

        foreach (var node in elements.OfType<Node>().ToList())
            graph.RemoveNode(node);
    }
}
