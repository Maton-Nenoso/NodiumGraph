using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// The primary graph editor canvas control.
/// </summary>
public class NodiumGraphCanvas : TemplatedControl
{
    public static readonly StyledProperty<Graph?> GraphProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, Graph?>(nameof(Graph));

    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(ViewportZoom), 1.0);

    public static readonly StyledProperty<Point> ViewportOffsetProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, Point>(nameof(ViewportOffset));

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(MinZoom), 0.1);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(MaxZoom), 5.0);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(GridSize), 20.0);

    public static readonly StyledProperty<bool> SnapToGridProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(SnapToGrid));

    public static readonly StyledProperty<IDataTemplate?> NodeTemplateProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IDataTemplate?>(nameof(NodeTemplate));

    public static readonly StyledProperty<IDataTemplate?> PortTemplateProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IDataTemplate?>(nameof(PortTemplate));

    public static readonly StyledProperty<IConnectionStyle> DefaultConnectionStyleProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionStyle>(
            nameof(DefaultConnectionStyle), new ConnectionStyle());

    public static readonly StyledProperty<IConnectionRouter> ConnectionRouterProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionRouter>(
            nameof(ConnectionRouter), new BezierRouter());

    public static readonly StyledProperty<INodeInteractionHandler?> NodeHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, INodeInteractionHandler?>(nameof(NodeHandler));

    public static readonly StyledProperty<IConnectionHandler?> ConnectionHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionHandler?>(nameof(ConnectionHandler));

    public static readonly StyledProperty<ISelectionHandler?> SelectionHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, ISelectionHandler?>(nameof(SelectionHandler));

    public static readonly StyledProperty<ICanvasInteractionHandler?> CanvasHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, ICanvasInteractionHandler?>(nameof(CanvasHandler));

    public static readonly StyledProperty<IConnectionValidator?> ConnectionValidatorProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionValidator?>(nameof(ConnectionValidator));

    public static readonly StyledProperty<bool> ShowMinimapProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowMinimap));

    public static readonly StyledProperty<MinimapPosition> MinimapPositionProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, MinimapPosition>(
            nameof(MinimapPosition), MinimapPosition.BottomRight);

    public Graph? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public double ViewportZoom
    {
        get => GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    public Point ViewportOffset
    {
        get => GetValue(ViewportOffsetProperty);
        set => SetValue(ViewportOffsetProperty, value);
    }

    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public bool SnapToGrid
    {
        get => GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public IDataTemplate? NodeTemplate
    {
        get => GetValue(NodeTemplateProperty);
        set => SetValue(NodeTemplateProperty, value);
    }

    public IDataTemplate? PortTemplate
    {
        get => GetValue(PortTemplateProperty);
        set => SetValue(PortTemplateProperty, value);
    }

    public IConnectionStyle DefaultConnectionStyle
    {
        get => GetValue(DefaultConnectionStyleProperty);
        set => SetValue(DefaultConnectionStyleProperty, value);
    }

    public IConnectionRouter ConnectionRouter
    {
        get => GetValue(ConnectionRouterProperty);
        set => SetValue(ConnectionRouterProperty, value);
    }

    public INodeInteractionHandler? NodeHandler
    {
        get => GetValue(NodeHandlerProperty);
        set => SetValue(NodeHandlerProperty, value);
    }

    public IConnectionHandler? ConnectionHandler
    {
        get => GetValue(ConnectionHandlerProperty);
        set => SetValue(ConnectionHandlerProperty, value);
    }

    public ISelectionHandler? SelectionHandler
    {
        get => GetValue(SelectionHandlerProperty);
        set => SetValue(SelectionHandlerProperty, value);
    }

    public ICanvasInteractionHandler? CanvasHandler
    {
        get => GetValue(CanvasHandlerProperty);
        set => SetValue(CanvasHandlerProperty, value);
    }

    public IConnectionValidator? ConnectionValidator
    {
        get => GetValue(ConnectionValidatorProperty);
        set => SetValue(ConnectionValidatorProperty, value);
    }

    public bool ShowMinimap
    {
        get => GetValue(ShowMinimapProperty);
        set => SetValue(ShowMinimapProperty, value);
    }

    public MinimapPosition MinimapPosition
    {
        get => GetValue(MinimapPositionProperty);
        set => SetValue(MinimapPositionProperty, value);
    }
}
