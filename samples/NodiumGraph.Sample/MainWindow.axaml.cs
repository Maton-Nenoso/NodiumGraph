using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
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

        // ── 1. Input Source ─────────────────────────────────────────────
        // Rectangle shape (default), AnglePortProvider, 1 output at 90°, green header
        var inputNode = new Node
        {
            Title = "Input Source",
            X = 100,
            Y = 200,
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(76, 175, 80))
            }
        };
        var inputOut = new Port(inputNode, "out", PortFlow.Output, default) { Angle = 90, Label = "out" };
        var inputProvider = new AnglePortProvider();
        inputProvider.AddPort(inputOut);
        inputNode.PortProvider = inputProvider;

        // ── 2. Transform ────────────────────────────────────────────────
        // RoundedRectangleShape(8), 1 input at 270°, 2 outputs at 45° and 135°,
        // blue header with custom border, diamond-shaped output ports
        var transformNode = new Node
        {
            Title = "Transform",
            X = 350,
            Y = 150,
            Shape = new RoundedRectangleShape(8),
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(66, 133, 244)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 80, 160)),
                BorderThickness = 2
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

        // ── 3. Filter ───────────────────────────────────────────────────
        // EllipseShape, 1 input at 270°, 1 output at 90°, orange header with opacity
        var filterNode = new Node
        {
            Title = "Filter",
            X = 350,
            Y = 350,
            Shape = new EllipseShape(),
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                Opacity = 0.85
            }
        };
        var filterIn = new Port(filterNode, "in", PortFlow.Input, default) { Angle = 270, Label = "in" };
        var filterOut = new Port(filterNode, "out", PortFlow.Output, default) { Angle = 90, Label = "out" };
        var filterProvider = new AnglePortProvider();
        filterProvider.AddPort(filterIn);
        filterProvider.AddPort(filterOut);
        filterNode.PortProvider = filterProvider;

        // ── 4. Merge ────────────────────────────────────────────────────
        // Rectangle shape, 2 inputs at 225° and 315°, 1 output at 90°,
        // ShowHeader = false, purple body
        var mergeNode = new Node
        {
            Title = "Merge",
            X = 600,
            Y = 250,
            ShowHeader = false,
            Style = new NodeStyle
            {
                BodyBackground = new SolidColorBrush(Color.FromRgb(156, 39, 176))
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

        // ── 5. Output Sink ──────────────────────────────────────────────
        // Rectangle shape, FixedPortProvider with 1 input on left, red header
        var outputNode = new Node
        {
            Title = "Output Sink",
            X = 850,
            Y = 250,
            Style = new NodeStyle
            {
                HeaderBackground = new SolidColorBrush(Color.FromRgb(244, 67, 54))
            }
        };
        var outputIn = new Port(outputNode, "in", PortFlow.Input, new Point(0, 15)) { Label = "in" };
        outputNode.PortProvider = new FixedPortProvider(new[] { outputIn });

        // ── 6. Comment ──────────────────────────────────────────────────
        var comment = new CommentNode { X = 100, Y = 50 };
        comment.Comment = "This is the pipeline entry point";

        // ── Add nodes to graph ──────────────────────────────────────────
        graph.AddNode(inputNode);
        graph.AddNode(transformNode);
        graph.AddNode(filterNode);
        graph.AddNode(mergeNode);
        graph.AddNode(outputNode);
        graph.AddNode(comment);

        // ── Connections ─────────────────────────────────────────────────
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

        Canvas.Graph = graph;

        // Set a custom node template with rich body content
        Canvas.NodeTemplate = CreateNodeTemplate(
            inputNode, transformNode, filterNode, mergeNode, outputNode);

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
            transformNode.IsCollapsed = CollapseTransformToggle.IsChecked == true;
        };

        ShowMergeHeaderToggle.IsCheckedChanged += (_, _) =>
        {
            mergeNode.ShowHeader = ShowMergeHeaderToggle.IsChecked == true;
        };
    }
    /// <summary>
    /// Creates a custom FuncDataTemplate that renders nodes with rich body content.
    /// CommentNode is handled inline since FuncDataTemplate&lt;Node&gt; matches all Node subtypes.
    /// </summary>
    private static IDataTemplate CreateNodeTemplate(
        Node inputNode, Node transformNode, Node filterNode, Node mergeNode, Node outputNode)
    {
        return new FuncDataTemplate<Node>((node, _) =>
        {
            // CommentNode: render the same way as the built-in CommentNodeTemplate
            if (node is CommentNode)
            {
                return new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 220, 100)),
                    Padding = new Thickness(8),
                    Child = new TextBlock
                    {
                        [!TextBlock.TextProperty] = new Binding(nameof(CommentNode.Comment)),
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 220, 100)),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 200
                    }
                };
            }

            var style = node?.Style;
            var cornerRadius = style?.CornerRadius ?? new CornerRadius(6);

            // Header bar
            var header = new Border
            {
                CornerRadius = new CornerRadius(cornerRadius.TopLeft, cornerRadius.TopRight, 0, 0),
                Padding = new Thickness(8, 4),
                [!Visual.IsVisibleProperty] = new Binding(nameof(Node.ShowHeader)),
                Child = new TextBlock
                {
                    [!TextBlock.TextProperty] = new Binding(nameof(Node.Title)),
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 12
                }
            };

            var headerText = (TextBlock)header.Child;
            if (style?.HeaderForeground != null)
                headerText.Foreground = style.HeaderForeground;
            else
            {
                headerText.Foreground = Brushes.White;
                headerText.Bind(TextBlock.ForegroundProperty,
                    headerText.GetResourceObservable(NodiumGraphResources.NodeHeaderForegroundBrushKey));
            }

            if (style?.HeaderBackground != null)
                header.Background = style.HeaderBackground;
            else
                header.Bind(Border.BackgroundProperty,
                    header.GetResourceObservable(NodiumGraphResources.NodeHeaderBrushKey));

            // Body with custom content
            var bodyContent = BuildBodyContent(node!, inputNode, transformNode, filterNode, mergeNode, outputNode);
            var body = new Border
            {
                MinHeight = 4,
                Padding = bodyContent != null ? new Thickness(8, 6) : new Thickness(0),
                Child = bodyContent,
                [!Visual.IsVisibleProperty] = new Binding(nameof(Node.IsCollapsed))
                {
                    Converter = InvertBoolConverter.Instance
                }
            };

            // Pill indicator for headerless collapsed nodes
            var pill = new Border
            {
                Height = 8,
                MinWidth = 40,
                CornerRadius = new CornerRadius(4),
                IsVisible = false
            };

            if (style?.HeaderBackground != null)
                pill.Background = style.HeaderBackground;
            else
                pill.Bind(Border.BackgroundProperty,
                    pill.GetResourceObservable(NodiumGraphResources.NodeHeaderBrushKey));

            pill.Bind(Visual.IsVisibleProperty, new MultiBinding
            {
                Converter = BoolConverters.And,
                Bindings =
                {
                    new Binding(nameof(Node.IsCollapsed)),
                    new Binding(nameof(Node.ShowHeader)) { Converter = InvertBoolConverter.Instance }
                }
            });

            var border = new Border
            {
                CornerRadius = cornerRadius,
                BorderThickness = new Thickness(style?.BorderThickness ?? 1),
                MinWidth = 130,
                Child = new StackPanel
                {
                    Children =
                    {
                        header,
                        body,
                        pill
                    }
                }
            };

            if (style?.BodyBackground != null)
                border.Background = style.BodyBackground;
            else
                border.Bind(Border.BackgroundProperty,
                    border.GetResourceObservable(NodiumGraphResources.NodeBodyBrushKey));

            if (style?.BorderBrush != null)
                border.BorderBrush = style.BorderBrush;
            else
                border.Bind(Border.BorderBrushProperty,
                    border.GetResourceObservable(NodiumGraphResources.NodeBorderBrushKey));

            if (style?.Opacity != null)
                border.Opacity = style.Opacity.Value;

            return border;
        }, supportsRecycling: false);
    }

    /// <summary>
    /// Builds the body content control for a specific node.
    /// Returns null for nodes without custom body content.
    /// </summary>
    private static Control? BuildBodyContent(
        Node node, Node inputNode, Node transformNode, Node filterNode, Node mergeNode, Node outputNode)
    {
        var labelColor = new SolidColorBrush(Color.FromRgb(160, 160, 160));
        var valueColor = new SolidColorBrush(Color.FromRgb(210, 210, 210));

        if (node == inputNode)
        {
            // Input Source: description + format info
            return new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Reads data from external source",
                        Foreground = labelColor,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = "Format:", Foreground = labelColor, FontSize = 11 },
                            new TextBlock { Text = "CSV", Foreground = valueColor, FontSize = 11, FontWeight = FontWeight.SemiBold }
                        }
                    }
                }
            };
        }

        if (node == transformNode)
        {
            // Transform: property rows
            return new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    CreatePropertyRow("Operation", "Map", labelColor, valueColor),
                    CreatePropertyRow("Parallel", "true", labelColor, valueColor)
                }
            };
        }

        if (node == filterNode)
        {
            // Filter: slider with label
            var valueText = new TextBlock
            {
                Text = "0.5",
                Foreground = valueColor,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0.5,
                SmallChange = 0.1,
                Width = 110
            };
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    valueText.Text = slider.Value.ToString("F2");
            };

            return new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock { Text = "Threshold", Foreground = labelColor, FontSize = 11 },
                        }
                    },
                    slider,
                    valueText
                }
            };
        }

        if (node == mergeNode)
        {
            // Merge: header-less node, body is the whole node
            return new TextBlock
            {
                Text = "Combines multiple inputs",
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        if (node == outputNode)
        {
            // Output Sink: property + checkbox
            return new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    CreatePropertyRow("Destination", "stdout", labelColor, valueColor),
                    new CheckBox
                    {
                        Content = "Append mode",
                        FontSize = 11,
                        Foreground = labelColor,
                        IsChecked = false
                    }
                }
            };
        }

        return null;
    }

    /// <summary>
    /// Creates a label: value row for property display in node bodies.
    /// </summary>
    private static StackPanel CreatePropertyRow(string label, string value, IBrush labelBrush, IBrush valueBrush)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label + ":", Foreground = labelBrush, FontSize = 11 },
                new TextBlock { Text = value, Foreground = valueBrush, FontSize = 11, FontWeight = FontWeight.SemiBold }
            }
        };
    }
}

file class InvertBoolConverter : IValueConverter
{
    public static readonly InvertBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
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
