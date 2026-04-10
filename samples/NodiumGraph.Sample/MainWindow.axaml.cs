using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
        // Rectangle shape (default), AnglePortProvider, 1 output at 90 deg, green header
        var inputNode = new InputSourceNode
        {
            Title = "Input Source",
            X = 100,
            Y = 200,
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            }
        };
        var inputOut = new Port(inputNode, "out", PortFlow.Output, default) { Angle = 90, Label = "out" };
        var inputProvider = new AnglePortProvider();
        inputProvider.AddPort(inputOut);
        inputNode.PortProvider = inputProvider;

        // -- 2. Transform --
        // RoundedRectangleShape(8), 1 input at 270 deg, 2 outputs at 45 deg and 135 deg,
        // blue header with custom border, diamond-shaped output ports
        var transformNode = new TransformNode
        {
            Title = "Transform",
            X = 350,
            Y = 150,
            IsCollapsible = true,
            Shape = new RoundedRectangleShape(8),
            Style = new NodeStyle
            {
                HeaderBackground = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(99, 102, 241), 0),
                        new GradientStop(Color.FromRgb(139, 92, 246), 1)
                    }
                }
            }
        };
        var diamondStyle = new PortStyle { Shape = PortShape.Diamond };
        var transformIn = new Port(transformNode, "in", PortFlow.Input, default) { Angle = 270, Label = "in" };
        var transformOut1 = new Port(transformNode, "out1", PortFlow.Output, default) { Angle = 45, Label = "out1", Style = diamondStyle };
        var transformOut2 = new Port(transformNode, "out2", PortFlow.Output, default) { Angle = 135, Label = "out2", Style = diamondStyle };
        var transformProvider = new AnglePortProvider();
        transformProvider.AddPort(transformIn);
        transformProvider.AddPort(transformOut1);
        transformProvider.AddPort(transformOut2);
        transformNode.PortProvider = transformProvider;

        // -- 3. Filter --
        // EllipseShape, 1 input at 270 deg, 1 output at 90 deg, orange header with opacity
        var filterNode = new FilterNode
        {
            Title = "Filter",
            X = 350,
            Y = 350,
            IsCollapsible = true,
            Shape = new EllipseShape(),
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(245, 158, 11))
            }
        };
        var filterIn = new Port(filterNode, "in", PortFlow.Input, default) { Angle = 270, Label = "in" };
        var filterOut = new Port(filterNode, "out", PortFlow.Output, default) { Angle = 90, Label = "out" };
        var filterProvider = new AnglePortProvider();
        filterProvider.AddPort(filterIn);
        filterProvider.AddPort(filterOut);
        filterNode.PortProvider = filterProvider;

        // -- 4. Merge --
        // Rectangle shape, 2 inputs at 225 deg and 315 deg, 1 output at 90 deg,
        // ShowHeader = false, purple body
        var mergeNode = new MergeNode
        {
            Title = "Merge",
            X = 600,
            Y = 250,
            ShowHeader = false,
            Style = new NodeStyle
            {
                BodyBackground = new SolidColorBrush(Color.FromRgb(139, 92, 246))
            }
        };
        var mergeIn1 = new Port(mergeNode, "input1", PortFlow.Input, default) { Angle = 225, Label = "input1" };
        var mergeIn2 = new Port(mergeNode, "input2", PortFlow.Input, default) { Angle = 315, Label = "input2" };
        var mergeOut = new Port(mergeNode, "out", PortFlow.Output, default) { Angle = 90, Label = "out" };
        var mergeProvider = new AnglePortProvider();
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
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(239, 68, 68))
            }
        };
        var outputIn = new Port(outputNode, "in", PortFlow.Input, new Point(0, 15)) { Label = "in" };
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
        // Transform -> Filter (out1 -> in)
        graph.AddConnection(new Connection(transformOut1, filterIn));
        // Transform -> Merge (out2 -> input1)
        graph.AddConnection(new Connection(transformOut2, mergeIn1));
        // Filter -> Merge (out -> input2)
        graph.AddConnection(new Connection(filterOut, mergeIn2));
        // Merge -> Output (out -> in)
        graph.AddConnection(new Connection(mergeOut, outputIn));

        // Don't set Canvas.NodeTemplate — let DataTemplate resolution from Window work
        Canvas.Graph = graph;

        // Wire a simple connection handler
        Canvas.ConnectionHandler = new SampleConnectionHandler(graph);
        Canvas.ConnectionValidator = new SampleConnectionValidator();

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

file class SampleConnectionValidator : IConnectionValidator
{
    public bool CanConnect(Port source, Port target)
    {
        if (source.Flow == target.Flow) return false;
        if (source.Owner == target.Owner) return false;
        return true;
    }
}
