using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// The primary graph editor canvas control.
/// </summary>
public class NodiumGraphCanvas : TemplatedControl, Avalonia.Rendering.ICustomHitTest
{
    static NodiumGraphCanvas()
    {
        ClipToBoundsProperty.OverrideDefaultValue<NodiumGraphCanvas>(true);
        FocusableProperty.OverrideDefaultValue<NodiumGraphCanvas>(true);
        DragDrop.AllowDropProperty.OverrideDefaultValue<NodiumGraphCanvas>(true);
        DragDrop.DropEvent.AddClassHandler<NodiumGraphCanvas>((canvas, e) => canvas.OnDrop(e));
    }

    public NodiumGraphCanvas()
    {
        _overlay = new CanvasOverlay(this);
        _overlay.ZIndex = int.MaxValue;
        VisualChildren.Add(_overlay);
        LogicalChildren.Add(_overlay);
    }

    private const double AutoPanMargin = 40.0;
    private const double AutoPanSpeed = 10.0;

    private readonly Dictionary<Node, ContentControl> _nodeContainers = new();
    private Node? _hoveredNode;
    private bool _isPanning;
    private bool _isSpaceHeld;
    private Point _panStartScreen;
    private Point _panStartOffset;
    private bool _isDragging;
    private Point _dragStartWorld;
    private Dictionary<Node, Point>? _dragStartPositions;
    private Node? _dragPrimaryNode;
    private Point? _snapGhostPosition;
    private Size _snapGhostSize;
    private bool _isDrawingConnection;
    private Port? _connectionSourcePort;
    private Port? _connectionTargetPort;
    private Point _connectionPreviewEnd;
    private bool _connectionPreviewValid;
    private bool _isCuttingConnections;
    private Point _cuttingStart;
    private Point _cuttingEnd;
    private bool _isMarqueeSelecting;
    private Point _marqueeStart;
    private Point _marqueeEnd;
    private bool _fallbackTemplatesRegistered;
    private IPortProvider? _commitProvider;
    private readonly Dictionary<IPortProvider, Action<Port>> _providerAddedHandlers = new();
    private readonly Dictionary<IPortProvider, Action<Port>> _providerRemovedHandlers = new();

    // Extra space around each node container so box shadows aren't clipped
    private const double ShadowPadding = 20;

    // Fallback defaults (used when theme resources not found)
    internal static readonly SolidColorBrush DefaultGridBrush = new(Color.FromArgb(40, 128, 128, 128));
    internal static readonly SolidColorBrush DefaultMajorGridBrush = new(Color.FromArgb(80, 128, 128, 128));
    internal static readonly SolidColorBrush DefaultOriginXAxisBrush = new(Color.FromArgb(96, 224, 80, 80));
    internal static readonly SolidColorBrush DefaultOriginYAxisBrush = new(Color.FromArgb(96, 80, 176, 80));
    internal static readonly SolidColorBrush DefaultPortBrush = new(Color.FromRgb(160, 160, 170));
    internal static readonly SolidColorBrush DefaultPortOutlineBrush = new(Colors.White);
    internal static readonly SolidColorBrush DefaultPreviewValidBrush = new(Colors.Green);
    internal static readonly SolidColorBrush DefaultPreviewInvalidBrush = new(Colors.Gray);
    internal static readonly SolidColorBrush DefaultCuttingBrush = new(Colors.Red);
    internal static readonly SolidColorBrush DefaultMarqueeFillBrush = new(Color.FromArgb(30, 100, 150, 255));
    internal static readonly SolidColorBrush DefaultMarqueeBorderBrush = new(Color.FromArgb(150, 100, 150, 255));
    internal static readonly SolidColorBrush DefaultSelectedBorderBrush = new(Color.FromRgb(80, 160, 255));
    internal static readonly SolidColorBrush DefaultHoveredBorderBrush = new(Color.FromArgb(120, 150, 190, 255));
    internal static readonly SolidColorBrush DefaultPortLabelBrush = new(Color.FromRgb(220, 220, 220));
    internal static readonly SolidColorBrush DefaultMinimapBackgroundBrush = new(Color.FromArgb(200, 30, 30, 30));
    internal static readonly SolidColorBrush DefaultMinimapNodeBrush = new(Color.FromArgb(180, 100, 150, 200));
    internal static readonly SolidColorBrush DefaultMinimapSelectedNodeBrush = new(Color.FromArgb(220, 80, 180, 255));
    internal static readonly SolidColorBrush DefaultMinimapViewportBrush = new(Color.FromArgb(150, 255, 255, 255));

    /// <summary>
    /// Resolves a brush from the Avalonia resource tree, falling back to a default.
    /// </summary>
    internal IBrush ResolveBrush(string key, IBrush fallback)
    {
        if (this.TryFindResource(key, out var resource) && resource is IBrush brush)
            return brush;
        return fallback;
    }

    /// <summary>
    /// Resolves a pen by looking up its brush from the resource tree with a fallback.
    /// </summary>
    internal Pen ResolvePen(string brushKey, IBrush fallbackBrush, double thickness, IDashStyle? dashStyle = null)
    {
        var brush = ResolveBrush(brushKey, fallbackBrush);
        return new Pen(brush, thickness, dashStyle);
    }

    private readonly CanvasOverlay _overlay;

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

    public static readonly StyledProperty<GridStyle> GridStyleProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, GridStyle>(nameof(GridStyle), GridStyle.Dots);

    public static readonly StyledProperty<int> MajorGridIntervalProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, int>(nameof(MajorGridInterval), 5);

    public static readonly StyledProperty<bool> ShowOriginAxesProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowOriginAxes), true);

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

    public static readonly StyledProperty<bool> ShowSnapGhostProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowSnapGhost));

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

    public bool ShowSnapGhost
    {
        get => GetValue(ShowSnapGhostProperty);
        set => SetValue(ShowSnapGhostProperty, value);
    }

    public GridStyle GridStyle
    {
        get => GetValue(GridStyleProperty);
        set => SetValue(GridStyleProperty, value);
    }

    public int MajorGridInterval
    {
        get => GetValue(MajorGridIntervalProperty);
        set => SetValue(MajorGridIntervalProperty, value);
    }

    public bool ShowOriginAxes
    {
        get => GetValue(ShowOriginAxesProperty);
        set => SetValue(ShowOriginAxesProperty, value);
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

    internal int NodeContainerCount => _nodeContainers.Count;

    internal bool IsPanning => _isPanning;

    internal bool IsDragging => _isDragging;

    internal bool IsDrawingConnection => _isDrawingConnection;

    internal bool IsCuttingConnections => _isCuttingConnections;

    internal bool IsMarqueeSelecting => _isMarqueeSelecting;

    internal Node? HoveredNode => _hoveredNode;

    // Overlay state accessors
    internal Port? ConnectionSourcePort => _connectionSourcePort;
    internal Port? ConnectionTargetPort => _connectionTargetPort;
    internal Point ConnectionPreviewEnd => _connectionPreviewEnd;
    internal bool ConnectionPreviewValid => _connectionPreviewValid;
    internal Point CuttingStart => _cuttingStart;
    internal Point CuttingEnd => _cuttingEnd;
    internal Point MarqueeStart => _marqueeStart;
    internal Point MarqueeEnd => _marqueeEnd;
    internal Point? SnapGhostPosition => _snapGhostPosition;
    internal Size SnapGhostSize => _snapGhostSize;

    internal Port? ResolvePort(Point screenPosition, bool preview)
    {
        if (Graph == null) return null;
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        var worldPosition = transform.ScreenToWorld(screenPosition);

        foreach (var node in Graph.Nodes)
        {
            if (node.IsCollapsed) continue;
            if (node.PortProvider == null) continue;
            var port = node.PortProvider.ResolvePort(worldPosition, preview);
            if (port != null) return port;
        }
        return null;
    }

    internal Node? HitTestNode(Point screenPosition)
    {
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        Node? result = null;

        foreach (var (node, container) in _nodeContainers)
        {
            var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var nodeScreenSize = new Size(
                transform.WorldToScreen(node.Width),
                transform.WorldToScreen(node.Height));
            var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

            if (nodeRect.Contains(screenPosition))
                result = node; // keep last match (topmost)
        }

        return result;
    }

    internal void SelectNode(Node node, bool additive)
    {
        if (Graph is null) return;

        if (!additive)
        {
            foreach (var n in Graph.SelectedNodes.ToList())
            {
                n.IsSelected = false;
                Graph.Deselect(n);
            }
        }

        if (node.IsSelected && additive)
        {
            node.IsSelected = false;
            Graph.Deselect(node);
        }
        else
        {
            node.IsSelected = true;
            Graph.Select(node);
        }

        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
        InvalidateVisual();
    }

    internal void ClearSelection()
    {
        if (Graph is null) return;

        foreach (var node in Graph.SelectedNodes.ToList())
            node.IsSelected = false;

        Graph.ClearSelection();
        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
        InvalidateVisual();
    }

    public void SelectAll()
    {
        if (Graph == null) return;

        foreach (var node in Graph.Nodes)
        {
            node.IsSelected = true;
            Graph.Select(node);
        }

        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
        InvalidateVisual();
    }

    public void DeleteSelected()
    {
        if (Graph == null || Graph.SelectedNodes.Count == 0) return;

        var selectedNodes = Graph.SelectedNodes.ToList();
        var affectedConnections = Graph.Connections
            .Where(c => selectedNodes.Contains(c.SourcePort.Owner) ||
                        selectedNodes.Contains(c.TargetPort.Owner))
            .ToList();

        NodeHandler?.OnDeleteRequested(selectedNodes, affectedConnections);
    }

    public void ZoomToFit(double padding = 50.0)
    {
        if (Graph == null || Graph.Nodes.Count == 0) return;
        ZoomToNodes(Graph.Nodes, padding);
    }

    public void ZoomToNodes(IEnumerable<Node> nodes, double padding = 50.0)
    {
        var nodeList = nodes.ToList();
        if (nodeList.Count == 0) return;

        var minX = nodeList.Min(n => n.X);
        var minY = nodeList.Min(n => n.Y);
        var maxX = nodeList.Max(n => n.X + n.Width);
        var maxY = nodeList.Max(n => n.Y + n.Height);

        var worldWidth = maxX - minX;
        var worldHeight = maxY - minY;

        if (worldWidth <= 0 || worldHeight <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var scaleX = (Bounds.Width - 2 * padding) / worldWidth;
        var scaleY = (Bounds.Height - 2 * padding) / worldHeight;
        var zoom = Math.Clamp(Math.Min(scaleX, scaleY), MinZoom, MaxZoom);

        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;

        ViewportZoom = zoom;
        ViewportOffset = new Point(
            Bounds.Width / 2 - centerX * zoom,
            Bounds.Height / 2 - centerY * zoom);
    }

    public void CenterOnNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var centerX = node.X + node.Width / 2;
        var centerY = node.Y + node.Height / 2;

        ViewportOffset = new Point(
            Bounds.Width / 2 - centerX * ViewportZoom,
            Bounds.Height / 2 - centerY * ViewportZoom);
    }

    internal void ApplyAutoPan(Point screenPosition)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var dx = 0.0;
        var dy = 0.0;

        if (screenPosition.X < AutoPanMargin)
            dx = AutoPanSpeed;
        else if (screenPosition.X > Bounds.Width - AutoPanMargin)
            dx = -AutoPanSpeed;

        if (screenPosition.Y < AutoPanMargin)
            dy = AutoPanSpeed;
        else if (screenPosition.Y > Bounds.Height - AutoPanMargin)
            dy = -AutoPanSpeed;

        if (dx != 0 || dy != 0)
        {
            ViewportOffset = new Point(ViewportOffset.X + dx, ViewportOffset.Y + dy);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var props = e.GetCurrentPoint(this).Properties;
        var position = e.GetPosition(this);

        if (e.ClickCount == 2 && props.IsLeftButtonPressed)
        {
            var dblClickNode = HitTestNode(position);
            if (dblClickNode != null)
            {
                NodeHandler?.OnNodeDoubleClicked(dblClickNode);
                e.Handled = true;
                return;
            }
            else
            {
                var dblTransform = new ViewportTransform(ViewportZoom, ViewportOffset);
                var worldPos = dblTransform.ScreenToWorld(position);
                CanvasHandler?.OnCanvasDoubleClicked(worldPos);
                e.Handled = true;
                return;
            }
        }

        if (ShowMinimap && Graph != null && props.IsLeftButtonPressed)
        {
            var worldPos = MinimapRenderer.MinimapToWorld(position, Bounds, Graph, MinimapPosition);
            if (worldPos.HasValue)
            {
                ViewportOffset = new Point(
                    Bounds.Width / 2 - worldPos.Value.X * ViewportZoom,
                    Bounds.Height / 2 - worldPos.Value.Y * ViewportZoom);
                e.Handled = true;
                return;
            }
        }

        if (props.IsMiddleButtonPressed ||
            (props.IsLeftButtonPressed && _isSpaceHeld))
        {
            _isPanning = true;
            _panStartScreen = position;
            _panStartOffset = ViewportOffset;
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed && (e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            _isCuttingConnections = true;
            _cuttingStart = position;
            _cuttingEnd = position;
            e.Handled = true;
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // Check port hit first (ports are on top of nodes)
        var hitPort = ResolvePort(position, preview: true);
        if (hitPort != null)
        {
            _isDrawingConnection = true;
            _connectionSourcePort = hitPort;
            _connectionPreviewEnd = position;
            _connectionPreviewValid = false;
            e.Handled = true;
            return;
        }

        var hitNode = HitTestNode(position);
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        if (hitNode != null)
        {
            if (!hitNode.IsSelected)
                SelectNode(hitNode, isCtrl);

            _isDragging = true;
            _dragPrimaryNode = hitNode;
            var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
            _dragStartWorld = transform.ScreenToWorld(position);
            _dragStartPositions = Graph!.SelectedNodes.ToDictionary(
                n => n,
                n => new Point(n.X, n.Y));
            e.Handled = true;
        }
        else
        {
            if (!isCtrl)
                ClearSelection();

            _isMarqueeSelecting = true;
            _marqueeStart = position;
            _marqueeEnd = position;
        }

        Focus();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isMarqueeSelecting)
        {
            _marqueeEnd = e.GetPosition(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isCuttingConnections)
        {
            _cuttingEnd = e.GetPosition(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDrawingConnection && _connectionSourcePort != null)
        {
            _connectionPreviewEnd = e.GetPosition(this);

            // Check if hovering over a valid target port
            var targetPort = ResolvePort(_connectionPreviewEnd, preview: true);
            _connectionTargetPort = targetPort != _connectionSourcePort ? targetPort : null;
            _connectionPreviewValid = _connectionTargetPort != null &&
                (ConnectionValidator?.CanConnect(_connectionSourcePort, _connectionTargetPort) ?? true);

            ApplyAutoPan(_connectionPreviewEnd);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDragging && _dragStartPositions != null)
        {
            var position = e.GetPosition(this);
            var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
            var currentWorld = transform.ScreenToWorld(position);
            var delta = currentWorld - _dragStartWorld;

            // Clear ghost by default each frame
            _snapGhostPosition = null;

            foreach (var (node, startPos) in _dragStartPositions)
            {
                var newX = startPos.X + delta.X;
                var newY = startPos.Y + delta.Y;

                if (SnapToGrid && GridSize > 0)
                {
                    if (ShowSnapGhost)
                    {
                        // Smooth drag: node follows cursor without snapping
                        // Compute snapped position for ghost on primary node only
                        if (node == _dragPrimaryNode)
                        {
                            var snappedX = Math.Round(newX / GridSize) * GridSize;
                            var snappedY = Math.Round(newY / GridSize) * GridSize;

                            // Show ghost only when snapped differs from actual
                            if (Math.Abs(snappedX - newX) > 0.001 || Math.Abs(snappedY - newY) > 0.001)
                            {
                                _snapGhostPosition = new Point(snappedX, snappedY);
                                _snapGhostSize = new Size(node.Width, node.Height);
                            }
                        }
                    }
                    else
                    {
                        // Original behavior: node jumps to grid
                        newX = Math.Round(newX / GridSize) * GridSize;
                        newY = Math.Round(newY / GridSize) * GridSize;
                    }
                }

                node.X = newX;
                node.Y = newY;
            }

            ApplyAutoPan(position);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            var position = e.GetPosition(this);
            var delta = position - _panStartScreen;
            ViewportOffset = new Point(_panStartOffset.X + delta.X, _panStartOffset.Y + delta.Y);
            e.Handled = true;
            return;
        }

        // Hover tracking (no active interaction)
        var hoverPos = e.GetPosition(this);
        var newHovered = HitTestNode(hoverPos);
        if (newHovered != _hoveredNode)
        {
            _hoveredNode = newHovered;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;

            if (Graph != null)
            {
                var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
                var marqueeRect = new Rect(
                    Math.Min(_marqueeStart.X, _marqueeEnd.X),
                    Math.Min(_marqueeStart.Y, _marqueeEnd.Y),
                    Math.Abs(_marqueeEnd.X - _marqueeStart.X),
                    Math.Abs(_marqueeEnd.Y - _marqueeStart.Y));

                foreach (var node in Graph.Nodes)
                {
                    var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
                    var nodeScreenSize = new Size(
                        transform.WorldToScreen(node.Width),
                        transform.WorldToScreen(node.Height));
                    var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

                    if (marqueeRect.Intersects(nodeRect))
                    {
                        node.IsSelected = true;
                        Graph.Select(node);
                    }
                }

                SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isCuttingConnections)
        {
            _isCuttingConnections = false;

            if (Graph != null && ConnectionHandler != null)
            {
                var transform = new ViewportTransform(ViewportZoom, ViewportOffset);

                foreach (var connection in Graph.Connections.ToList())
                {
                    if (CuttingLineIntersectsGeometry(_cuttingStart, _cuttingEnd, connection, transform))
                    {
                        ConnectionHandler.OnConnectionDeleteRequested(connection);
                    }
                }
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDrawingConnection && _connectionSourcePort != null)
        {
            var position = e.GetPosition(this);
            _commitProvider = null;
            Port? targetPort = null;

            if (Graph != null)
            {
                var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
                var worldPosition = transform.ScreenToWorld(position);

                foreach (var node in Graph.Nodes)
                {
                    if (node.IsCollapsed) continue;
                    if (node.PortProvider == null) continue;
                    var port = node.PortProvider.ResolvePort(worldPosition, preview: false);
                    if (port != null)
                    {
                        targetPort = port;
                        _commitProvider = node.PortProvider;
                        break;
                    }
                }
            }

            var connected = false;
            if (targetPort != null && targetPort != _connectionSourcePort)
            {
                var canConnect = ConnectionValidator?.CanConnect(_connectionSourcePort, targetPort) ?? true;
                if (canConnect)
                {
                    var result = ConnectionHandler?.OnConnectionRequested(_connectionSourcePort, targetPort);
                    if (result is { IsSuccess: true })
                        connected = true;
                }
            }

            if (!connected)
                _commitProvider?.CancelResolve();

            _commitProvider = null;
            _isDrawingConnection = false;
            _connectionSourcePort = null;
            _connectionTargetPort = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDragging && _dragStartPositions != null)
        {
            _isDragging = false;

            // Snap primary node to ghost position on release
            if (ShowSnapGhost && SnapToGrid && GridSize > 0 && _dragPrimaryNode != null)
            {
                var snappedX = Math.Round(_dragPrimaryNode.X / GridSize) * GridSize;
                var snappedY = Math.Round(_dragPrimaryNode.Y / GridSize) * GridSize;
                var snapDeltaX = snappedX - _dragPrimaryNode.X;
                var snapDeltaY = snappedY - _dragPrimaryNode.Y;

                // Apply the same snap delta to all selected nodes
                foreach (var (node, _) in _dragStartPositions)
                {
                    node.X += snapDeltaX;
                    node.Y += snapDeltaY;
                }
            }

            _snapGhostPosition = null;

            var moves = _dragStartPositions
                .Select(kvp => new NodeMoveInfo(
                    kvp.Key,
                    kvp.Value,
                    new Point(kvp.Key.X, kvp.Key.Y)))
                .Where(m => m.OldPosition != m.NewPosition)
                .ToList();

            if (moves.Count > 0)
                NodeHandler?.OnNodesMoved(moves);

            _dragStartPositions = null;
            _dragPrimaryNode = null;
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space)
        {
            _isSpaceHeld = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.A && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_isMarqueeSelecting)
            {
                _isMarqueeSelecting = false;
                InvalidateVisual();
            }
            else if (_isCuttingConnections)
            {
                _isCuttingConnections = false;
                InvalidateVisual();
            }
            else if (_isDrawingConnection)
            {
                _isDrawingConnection = false;
                _connectionSourcePort = null;
                InvalidateVisual();
            }
            else if (_isDragging)
            {
                // Cancel drag — restore original positions
                if (_dragStartPositions != null)
                {
                    foreach (var (node, startPos) in _dragStartPositions)
                    {
                        node.X = startPos.X;
                        node.Y = startPos.Y;
                    }

                    _dragStartPositions = null;
                }

                _isDragging = false;
                _dragPrimaryNode = null;
                _snapGhostPosition = null;
                InvalidateVisual();
            }
            else
            {
                ClearSelection();
            }

            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key == Key.Space)
            _isSpaceHeld = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var cursorScreen = e.GetPosition(this);
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        var cursorWorld = transform.ScreenToWorld(cursorScreen);

        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        var newZoom = Math.Clamp(ViewportZoom * zoomFactor, MinZoom, MaxZoom);

        // Adjust offset so cursor world point stays fixed
        ViewportZoom = newZoom;
        ViewportOffset = new Point(
            cursorScreen.X - cursorWorld.X * newZoom,
            cursorScreen.Y - cursorWorld.Y * newZoom);

        e.Handled = true;
    }

    private void OnDrop(DragEventArgs e)
    {
        if (CanvasHandler == null) return;
        var position = e.GetPosition(this);
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        var worldPos = transform.ScreenToWorld(position);
        CanvasHandler.OnCanvasDropped(worldPos, e.DataTransfer);
        e.Handled = true;
    }

    public new void InvalidateVisual()
    {
        base.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredNode != null)
        {
            _hoveredNode = null;
            InvalidateVisual();
        }
    }

    // ICustomHitTest makes the canvas hit-testable across its entire area,
    // even without a ControlTemplate. Without this, pointer/wheel events
    // over empty canvas space never reach the control.
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => true;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Draw background (TemplatedControl without a template needs this for visible background)
        if (Background is { } bg)
            context.DrawRectangle(bg, null, new Rect(Bounds.Size));

        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);

        if (ShowGrid)
        {
            var gridBrush = ResolveBrush(NodiumGraphResources.GridBrushKey, DefaultGridBrush);
            var majorBrush = ResolveBrush(NodiumGraphResources.MajorGridBrushKey, DefaultMajorGridBrush);
            GridRenderer.Render(context, new Rect(Bounds.Size), transform, GridSize, GridStyle, gridBrush, majorBrush, MajorGridInterval);
        }

        if (ShowOriginAxes)
        {
            var xAxisBrush = ResolveBrush(NodiumGraphResources.OriginXAxisBrushKey, DefaultOriginXAxisBrush);
            var yAxisBrush = ResolveBrush(NodiumGraphResources.OriginYAxisBrushKey, DefaultOriginYAxisBrush);
            GridRenderer.RenderOriginAxes(context, new Rect(Bounds.Size), transform, xAxisBrush, yAxisBrush);
        }

        if (Graph != null)
        {
            var router = ConnectionRouter;
            var style = DefaultConnectionStyle;
            var connectionPen = new Pen(style.Stroke, style.Thickness, style.DashPattern);
            foreach (var connection in Graph.Connections)
            {
                ConnectionRenderer.Render(context, connection, router, connectionPen, transform);
            }
        }

        // Ports, connection preview, cutting line, marquee, minimap
        // are drawn by _overlay (renders on top of node containers)
    }

    internal bool CuttingLineIntersectsGeometry(
        Point lineStart, Point lineEnd, Connection connection, ViewportTransform transform)
    {
        var routePoints = ConnectionRouter.Route(connection.SourcePort, connection.TargetPort);
        var screenPoints = routePoints.Select(transform.WorldToScreen).ToList();

        // For bezier (4 points), sample the curve at intervals for approximate intersection
        if (ConnectionRouter.IsBezierRoute && screenPoints.Count == 4)
        {
            var samples = new List<Point>();
            for (var t = 0.0; t <= 1.0; t += 0.05)
            {
                samples.Add(BezierPoint(screenPoints[0], screenPoints[1], screenPoints[2], screenPoints[3], t));
            }

            for (var i = 0; i < samples.Count - 1; i++)
            {
                if (LinesIntersect(lineStart, lineEnd, samples[i], samples[i + 1]))
                    return true;
            }

            return false;
        }

        // For polyline, check each segment
        for (var i = 0; i < screenPoints.Count - 1; i++)
        {
            if (LinesIntersect(lineStart, lineEnd, screenPoints[i], screenPoints[i + 1]))
                return true;
        }

        return false;
    }

    internal static Point BezierPoint(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        var x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
        var y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
        return new Point(x, y);
    }

    internal static bool LinesIntersect(Point a1, Point a2, Point b1, Point b2)
    {
        var d1 = CrossProduct(b2 - b1, a1 - b1);
        var d2 = CrossProduct(b2 - b1, a2 - b1);
        var d3 = CrossProduct(a2 - a1, b1 - a1);
        var d4 = CrossProduct(a2 - a1, b2 - a1);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        // Collinear overlap check
        if (d1 == 0 && OnSegment(b1, a1, b2)) return true;
        if (d2 == 0 && OnSegment(b1, a2, b2)) return true;
        if (d3 == 0 && OnSegment(a1, b1, a2)) return true;
        if (d4 == 0 && OnSegment(a1, b2, a2)) return true;

        return false;
    }

    internal static bool OnSegment(Point p, Point q, Point r)
    {
        return q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) &&
               q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);
    }

    private static double CrossProduct(Vector a, Vector b) => a.X * b.Y - a.Y * b.X;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphProperty)
        {
            OnGraphChanged(change.GetOldValue<Graph?>(), change.GetNewValue<Graph?>());
            InvalidateVisual();
        }
        else if (change.Property == ViewportZoomProperty ||
                 change.Property == ViewportOffsetProperty)
        {
            InvalidateArrange();
            InvalidateVisual();
        }
        else if (change.Property == ShowGridProperty ||
                 change.Property == GridSizeProperty ||
                 change.Property == GridStyleProperty ||
                 change.Property == MajorGridIntervalProperty ||
                 change.Property == ShowOriginAxesProperty ||
                 change.Property == ConnectionRouterProperty ||
                 change.Property == DefaultConnectionStyleProperty ||
                 change.Property == ShowMinimapProperty ||
                 change.Property == MinimapPositionProperty)
        {
            InvalidateVisual();
        }
    }

    private void OnGraphChanged(Graph? oldGraph, Graph? newGraph)
    {
        if (oldGraph != null)
        {
            ((INotifyCollectionChanged)oldGraph.Nodes).CollectionChanged -= OnNodesCollectionChanged;
            ((INotifyCollectionChanged)oldGraph.Connections).CollectionChanged -= OnConnectionsCollectionChanged;
            foreach (var node in oldGraph.Nodes)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                if (node.PortProvider != null)
                    DetachProvider(node.PortProvider);
                if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
                    layoutAware.LayoutInvalidated -= OnLayoutAwareProviderInvalidated;
            }
        }

        foreach (var container in _nodeContainers.Values)
        {
            LogicalChildren.Remove(container);
            VisualChildren.Remove(container);
        }

        _nodeContainers.Clear();

        if (newGraph != null)
        {
            ((INotifyCollectionChanged)newGraph.Nodes).CollectionChanged += OnNodesCollectionChanged;
            ((INotifyCollectionChanged)newGraph.Connections).CollectionChanged += OnConnectionsCollectionChanged;
            foreach (var node in newGraph.Nodes)
                AddNodeContainer(node);
        }
    }

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (Node node in e.NewItems)
                AddNodeContainer(node);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (Node node in e.OldItems)
                RemoveNodeContainer(node);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var (node, container) in _nodeContainers)
            {
                node.PropertyChanged -= OnNodePropertyChanged;
                if (node.PortProvider != null)
                    DetachProvider(node.PortProvider);
                if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
                    layoutAware.LayoutInvalidated -= OnLayoutAwareProviderInvalidated;
                LogicalChildren.Remove(container);
                VisualChildren.Remove(container);
            }

            _nodeContainers.Clear();
        }
    }

    private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (Connection conn in e.OldItems)
            {
                NotifyProviderOfDisconnect(conn.SourcePort);
                NotifyProviderOfDisconnect(conn.TargetPort);
            }
        }
        InvalidateVisual();
    }

    private void NotifyProviderOfDisconnect(Port port)
    {
        if (Graph == null) return;
        if (port.Owner.PortProvider is DynamicPortProvider dynamicProvider)
            dynamicProvider.NotifyDisconnected(port, Graph);
    }

    private void AddNodeContainer(Node node)
    {
        var template = DefaultTemplates.ResolveTemplate(node, NodeTemplate);
        var container = new ContentControl
        {
            DataContext = node,
            Content = node,
            ClipToBounds = false,
        };

        if (template != null)
        {
            container.ContentTemplate = template;
        }
        else
        {
            // Custom subclass with no specific template — let DataTemplate resolution
            // walk the visual tree. Fallback templates are registered on the canvas
            // (not on each container) so that more-specific templates defined higher
            // in the tree (e.g. Window.DataTemplates) take priority.
            EnsureFallbackTemplates();
        }

        _nodeContainers[node] = container;
        LogicalChildren.Add(container);
        VisualChildren.Add(container);

        node.PropertyChanged += OnNodePropertyChanged;

        if (node.PortProvider != null)
            AttachProvider(node.PortProvider);

        if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
            layoutAware.LayoutInvalidated += OnLayoutAwareProviderInvalidated;

        InvalidateMeasure();
    }

    private void EnsureFallbackTemplates()
    {
        if (_fallbackTemplatesRegistered) return;
        _fallbackTemplatesRegistered = true;

        // Register on Application so it's checked LAST — after Window.DataTemplates
        // and any other templates defined higher in the visual tree.
        if (Application.Current is { } app)
            app.DataTemplates.Add(DefaultTemplates.NodeTemplate);
        else
            DataTemplates.Add(DefaultTemplates.NodeTemplate);
    }

    private void RemoveNodeContainer(Node node)
    {
        if (_nodeContainers.TryGetValue(node, out var container))
        {
            node.PropertyChanged -= OnNodePropertyChanged;

            if (node.PortProvider != null)
                DetachProvider(node.PortProvider);

            if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
                layoutAware.LayoutInvalidated -= OnLayoutAwareProviderInvalidated;

            LogicalChildren.Remove(container);
            VisualChildren.Remove(container);
            _nodeContainers.Remove(node);
            InvalidateMeasure();
        }
    }

    private void OnLayoutAwareProviderInvalidated()
    {
        InvalidateVisual();
    }

    private void AttachProvider(IPortProvider provider)
    {
        foreach (var port in provider.Ports)
            SubscribeToPort(port);

        Action<Port> onAdded = p => { SubscribeToPort(p); InvalidateVisual(); };
        Action<Port> onRemoved = p => { UnsubscribeFromPort(p); InvalidateVisual(); };
        provider.PortAdded += onAdded;
        provider.PortRemoved += onRemoved;
        _providerAddedHandlers[provider] = onAdded;
        _providerRemovedHandlers[provider] = onRemoved;
    }

    private void DetachProvider(IPortProvider provider)
    {
        foreach (var port in provider.Ports)
            UnsubscribeFromPort(port);

        if (_providerAddedHandlers.TryGetValue(provider, out var onAdded))
            provider.PortAdded -= onAdded;
        if (_providerRemovedHandlers.TryGetValue(provider, out var onRemoved))
            provider.PortRemoved -= onRemoved;
        _providerAddedHandlers.Remove(provider);
        _providerRemovedHandlers.Remove(provider);
    }

    private void SubscribeToPort(Port port)
    {
        port.PropertyChanged += OnPortPropertyChanged;
        if (port.Style != null)
            port.Style.PropertyChanged += OnPortStylePropertyChanged;
    }

    private void UnsubscribeFromPort(Port port)
    {
        port.PropertyChanged -= OnPortPropertyChanged;
        if (port.Style != null)
            port.Style.PropertyChanged -= OnPortStylePropertyChanged;
    }

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Port.AbsolutePosition) or nameof(Port.Label) or nameof(Port.Style))
            InvalidateVisual();
    }

    private void OnPortStylePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Node.X) or nameof(Node.Y))
            InvalidateArrange();
        else if (e.PropertyName is nameof(Node.IsSelected))
            InvalidateVisual();
        else if (e.PropertyName is nameof(Node.ShowHeader) or nameof(Node.IsCollapsed) or nameof(Node.IsCollapsible))
            InvalidateMeasure();
        else if (e.PropertyName == nameof(Node.PortProvider))
        {
            if (sender is Node node && node.PortProvider != null)
                AttachProvider(node.PortProvider);
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var (_, container) in _nodeContainers)
        {
            container.Measure(Size.Infinity);
        }

        _overlay.Measure(availableSize);

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);

        foreach (var (node, container) in _nodeContainers)
        {
            var desired = container.DesiredSize;

            // DesiredSize includes the shadow margin from the NodePresenter template.
            // Subtract it to get the actual node content dimensions.
            const double sp = ShadowPadding;
            var contentWidth = desired.Width - sp * 2;
            var contentHeight = desired.Height - sp * 2;
            if (contentWidth > 0) node.Width = contentWidth;
            if (contentHeight > 0) node.Height = contentHeight;

            if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
                layoutAware.UpdateLayout(node.Width, node.Height, node.Shape);

            var screenPos = transform.WorldToScreen(new Point(node.X, node.Y));

            // Arrange at full size (content + shadow margin). Offset position
            // so the visible node aligns with screenPos despite the margin.
            container.RenderTransform = new ScaleTransform(ViewportZoom, ViewportZoom);
            container.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
            var adjusted = new Point(screenPos.X - sp * ViewportZoom, screenPos.Y - sp * ViewportZoom);
            container.Arrange(new Rect(adjusted, desired));
        }

        // Overlay fills the entire canvas, renders on top of all node containers
        _overlay.Arrange(new Rect(finalSize));

        return finalSize;
    }
}
