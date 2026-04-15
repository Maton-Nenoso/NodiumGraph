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
public class NodiumGraphCanvas : TemplatedControl, Avalonia.Rendering.ICustomHitTest, IDisposable
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

    private readonly Dictionary<Node, NodiumNodeContainer> _nodeContainers = new();
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
    private static bool _fallbackTemplatesRegistered;
    private IPortProvider? _commitProvider;
    private IPortProvider? _sourceProvider;
    private readonly Dictionary<IPortProvider, Action<Port>> _providerAddedHandlers = new();
    private readonly Dictionary<IPortProvider, Action<Port>> _providerRemovedHandlers = new();
    private readonly Dictionary<Node, IPortProvider> _nodeProviders = new();
    private readonly Dictionary<Port, PortStyle?> _portStyles = new();
    private bool _disposed;

    // Sentinel-cached connection pen. Comparing the three IConnectionStyle
    // getter values (not the style instance) preserves in-place-mutation semantics.
    private Pen? _cachedConnectionPen;
    private IBrush? _lastConnectionStroke;
    private double _lastConnectionThickness;
    private IDashStyle? _lastConnectionDashPattern;

    // Shared render caches — used by CanvasOverlay (validation feedback, previews)
    // and by per-node NodeAdornmentLayer (selection border, ports, labels).
    // All caches below are UI-thread only — Avalonia render runs on the UI thread,
    // and no synchronization is applied. Do not touch them from background work.
    // Font size is bucketed to 0.5 px so continuous zoom reuses cache entries.
    // IBrush is compared by reference — in-place brush mutation leaves stale content.
    private const int LabelCacheMaxEntries = 256;
    private readonly Dictionary<(string label, double bucketedFontSize, IBrush brush), FormattedText> _labelCache
        = new(LabelCacheKeyComparer.Instance);

    // Pens held by reference-identity on brush; same stale-on-mutation caveat.
    private const int StyledPenCacheMaxEntries = 32;
    private readonly Dictionary<(IBrush brush, double thickness), Pen> _styledPenCache
        = new(BrushThicknessComparer.Instance);

    private const int PortGeometryCacheMaxEntries = 64;
    private readonly Dictionary<(PortShape shape, double bucketedRadius), Geometry> _portGeometryCache = new();

    // Single-slot cached pens — dirty-tracked by brush/thickness identity.
    private Pen? _cachedSelectedBorderPen;
    private IBrush? _lastSelectedBrush;
    private double _lastSelectedThickness;

    private Pen? _cachedHoveredBorderPen;
    private IBrush? _lastHoveredBrush;
    private double _lastHoveredThickness;

    private Pen? _cachedPortOutlinePen;
    private IBrush? _lastPortOutlineBrush;
    private double _lastPortOutlineThickness;

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
    /// Resolves a non-brush resource from the Avalonia resource tree, falling back to a default.
    /// </summary>
    internal T ResolveResource<T>(string key, T fallback)
    {
        if (this.TryFindResource(key, out var value) && value is T typed)
            return typed;
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
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionValidator?>(
            nameof(ConnectionValidator),
            defaultValue: DefaultConnectionValidator.Instance);

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

    /// <summary>
    /// Validator consulted during a connection drag to accept/reject drop targets.
    /// Defaults to <see cref="DefaultConnectionValidator.Instance"/>, which rejects self-loops,
    /// same-owner pairs, same-flow pairs, and mismatched <see cref="Port.DataType"/> values.
    /// Set to <c>null</c> to disable all built-in checks.
    /// </summary>
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

    internal NodiumNodeContainer? GetInternalNodeContainer(Node node)
        => _nodeContainers.TryGetValue(node, out var c) ? c : null;

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
        => ResolvePortWithProvider(screenPosition, preview).port;

    internal (Port? port, IPortProvider? provider) ResolvePortWithProvider(Point screenPosition, bool preview)
    {
        if (Graph == null) return (null, null);
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        var worldPosition = transform.ScreenToWorld(screenPosition);

        foreach (var node in Graph.Nodes)
        {
            if (node.IsCollapsed) continue;
            if (node.PortProvider == null) continue;
            var port = node.PortProvider.ResolvePort(worldPosition, preview);
            if (port != null) return (port, node.PortProvider);
        }
        return (null, null);
    }

    internal Node? HitTestNode(Point screenPosition)
    {
        if (Graph == null) return null;
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        Node? result = null;

        // Iterate Graph.Nodes (stable insertion order) instead of _nodeContainers (Dictionary).
        // Last match wins = topmost in z-order.
        foreach (var node in Graph.Nodes)
        {
            var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var nodeScreenSize = new Size(
                transform.WorldToScreen(node.Width),
                transform.WorldToScreen(node.Height));
            var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

            if (nodeRect.Contains(screenPosition))
                result = node;
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
                Graph.Deselect(n);
                InvalidateNodeAdornments(n);
            }
        }

        if (node.IsSelected && additive)
            Graph.Deselect(node);
        else
            Graph.Select(node);

        InvalidateNodeAdornments(node);
        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
    }

    internal void ClearSelection()
    {
        if (Graph is null) return;

        var previouslySelected = Graph.SelectedNodes.ToList();
        Graph.ClearSelection();
        foreach (var n in previouslySelected)
            InvalidateNodeAdornments(n);
        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
    }

    public void SelectAll()
    {
        if (Graph == null) return;

        foreach (var node in Graph.Nodes)
        {
            Graph.Select(node);
            InvalidateNodeAdornments(node);
        }

        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
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
        var (hitPort, sourceProvider) = ResolvePortWithProvider(position, preview: false);
        if (hitPort != null)
        {
            _isDrawingConnection = true;
            _connectionSourcePort = hitPort;
            _sourceProvider = sourceProvider;
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
            var previous = _hoveredNode;
            _hoveredNode = newHovered;
            if (previous != null)
                InvalidateNodeAdornments(previous);
            if (newHovered != null)
                InvalidateNodeAdornments(newHovered);
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
                        Graph.Select(node);
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
            Port? targetPort = null;

            try
            {
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
                {
                    _commitProvider?.CancelResolve();
                    _sourceProvider?.CancelResolve();
                }
            }
            finally
            {
                _commitProvider = null;
                _sourceProvider = null;
                _isDrawingConnection = false;
                _connectionSourcePort = null;
                _connectionTargetPort = null;
            }

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

    /// <summary>
    /// Invalidates only the per-node adornment layer (selection/hover border, port
    /// shapes, port labels) for the given node, without re-rendering the whole canvas.
    /// </summary>
    internal void InvalidateNodeAdornments(Node node)
    {
        if (_nodeContainers.TryGetValue(node, out var container))
            container.AdornmentLayer.InvalidateVisual();
    }

    internal void InvalidateAllNodeAdornments()
    {
        foreach (var container in _nodeContainers.Values)
            container.AdornmentLayer.InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredNode != null)
        {
            var previous = _hoveredNode;
            _hoveredNode = null;
            InvalidateNodeAdornments(previous);
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
            if (_cachedConnectionPen is null
                || !ReferenceEquals(_lastConnectionStroke, style.Stroke)
                || _lastConnectionThickness != style.Thickness
                || !ReferenceEquals(_lastConnectionDashPattern, style.DashPattern))
            {
                _cachedConnectionPen = new Pen(style.Stroke, style.Thickness, style.DashPattern);
                _lastConnectionStroke = style.Stroke;
                _lastConnectionThickness = style.Thickness;
                _lastConnectionDashPattern = style.DashPattern;
            }
            var connectionPen = _cachedConnectionPen;

            var topLeftWorld = transform.ScreenToWorld(new Point(0, 0));
            var bottomRightWorld = transform.ScreenToWorld(new Point(Bounds.Width, Bounds.Height));
            var zoom = ViewportZoom;
            var strokeBleed = zoom > 0 ? connectionPen.Thickness / zoom : connectionPen.Thickness;
            var viewportWorld = new Rect(
                topLeftWorld.X,
                topLeftWorld.Y,
                bottomRightWorld.X - topLeftWorld.X,
                bottomRightWorld.Y - topLeftWorld.Y).Inflate(strokeBleed);

            // Push the viewport transform once for the entire connection batch so
            // ConnectionRenderer can emit world-space geometry. Keeping geometry in
            // world space lets a future hit-test cache avoid thrashing on pan/zoom.
            var viewportMatrix = Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(transform.Offset.X, transform.Offset.Y);
            using (context.PushTransform(viewportMatrix))
            {
                foreach (var connection in Graph.Connections)
                {
                    // Route() is the single source of truth for geometry; bounds fall out of the
                    // returned points. Cubic beziers stay inside the convex hull of their control
                    // points, so the AABB of the route output is a valid conservative bound for
                    // bezier curves regardless of which direction control points push.
                    var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
                    if (routePoints.Count < 2) continue;

                    var bounds = ComputeRouteBounds(routePoints);
                    if (!viewportWorld.Intersects(bounds)) continue;

                    // Route once per frame: hand the already-computed points to the
                    // renderer so it doesn't call router.Route() a second time.
                    var renderable = ConnectionRenderer.CreateRenderable(routePoints, router.RouteKind, style);
                    ConnectionRenderer.Render(context, renderable, style, connectionPen);
                }
            }
        }

        // Ports, connection preview, cutting line, marquee, minimap
        // are drawn by _overlay (renders on top of node containers)
    }

    private static Rect ComputeRouteBounds(IReadOnlyList<Point> points)
    {
        var first = points[0];
        double minX = first.X, minY = first.Y, maxX = first.X, maxY = first.Y;
        for (var i = 1; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X < minX) minX = p.X;
            else if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            else if (p.Y > maxY) maxY = p.Y;
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    internal bool CuttingLineIntersectsGeometry(
        Point lineStart, Point lineEnd, Connection connection, ViewportTransform transform)
    {
        var routePoints = ConnectionRouter.Route(connection.SourcePort, connection.TargetPort);
        if (routePoints.Count < 2) return false;

        if (ConnectionRouter.RouteKind == RouteKind.Bezier && routePoints.Count == 4)
        {
            var p0 = transform.WorldToScreen(routePoints[0]);
            var p1 = transform.WorldToScreen(routePoints[1]);
            var p2 = transform.WorldToScreen(routePoints[2]);
            var p3 = transform.WorldToScreen(routePoints[3]);

            var prev = p0;
            for (var t = 0.05; t <= 1.0; t += 0.05)
            {
                var current = BezierPoint(p0, p1, p2, p3, t);
                if (LinesIntersect(lineStart, lineEnd, prev, current))
                    return true;
                prev = current;
            }
            return false;
        }

        // Polyline — iterate route points directly, no list allocation
        var prevScreen = transform.WorldToScreen(routePoints[0]);
        for (var i = 1; i < routePoints.Count; i++)
        {
            var currentScreen = transform.WorldToScreen(routePoints[i]);
            if (LinesIntersect(lineStart, lineEnd, prevScreen, currentScreen))
                return true;
            prevScreen = currentScreen;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Graph != null && _nodeContainers.Count == 0)
            OnGraphChanged(null, Graph);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_disposed) return;
        if (Graph != null)
            OnGraphChanged(Graph, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (Graph != null)
            OnGraphChanged(Graph, null);

        GC.SuppressFinalize(this);
    }

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

            // Adornment layers bake zoom into pen thickness, inflate, and corner
            // radius (they draw in node-local space under the container's
            // ScaleTransform(zoom), so values are divided by zoom to stay visually
            // constant). A zoom change invalidates those baked values and the
            // adornments must re-render. The compositor's transform reapply is
            // not enough on its own.
            if (change.Property == ViewportZoomProperty)
            {
                foreach (var container in _nodeContainers.Values)
                    container.AdornmentLayer.InvalidateVisual();
            }
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
                    DetachProvider(node, node.PortProvider);
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
                    DetachProvider(node, node.PortProvider);
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
        var container = new NodiumNodeContainer(this, node)
        {
            ContentTemplate = template,
        };

        if (template == null)
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
            AttachProvider(node, node.PortProvider);

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
                DetachProvider(node, node.PortProvider);

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
        // No node context on the parameterless event — invalidate all adornment
        // layers so port shapes and labels repaint at the new positions.
        InvalidateAllNodeAdornments();
    }

    private void AttachProvider(Node node, IPortProvider provider)
    {
        foreach (var port in provider.Ports)
            SubscribeToPort(port);

        Action<Port> onAdded = p =>
        {
            SubscribeToPort(p);
            InvalidateNodeAdornments(node);
        };
        Action<Port> onRemoved = p =>
        {
            UnsubscribeFromPort(p);
            InvalidateNodeAdornments(node);
        };
        provider.PortAdded += onAdded;
        provider.PortRemoved += onRemoved;
        _providerAddedHandlers[provider] = onAdded;
        _providerRemovedHandlers[provider] = onRemoved;
        _nodeProviders[node] = provider;
    }

    private void DetachProvider(Node node, IPortProvider provider)
    {
        foreach (var port in provider.Ports)
            UnsubscribeFromPort(port);

        if (_providerAddedHandlers.TryGetValue(provider, out var onAdded))
            provider.PortAdded -= onAdded;
        if (_providerRemovedHandlers.TryGetValue(provider, out var onRemoved))
            provider.PortRemoved -= onRemoved;
        _providerAddedHandlers.Remove(provider);
        _providerRemovedHandlers.Remove(provider);
        _nodeProviders.Remove(node);
    }

    private void SubscribeToPort(Port port)
    {
        port.PropertyChanged += OnPortPropertyChanged;
        if (port.Style != null)
            port.Style.PropertyChanged += OnPortStylePropertyChanged;
        _portStyles[port] = port.Style;
    }

    private void UnsubscribeFromPort(Port port)
    {
        port.PropertyChanged -= OnPortPropertyChanged;
        if (_portStyles.TryGetValue(port, out var trackedStyle) && trackedStyle != null)
            trackedStyle.PropertyChanged -= OnPortStylePropertyChanged;
        _portStyles.Remove(port);
    }

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Port.Style) && sender is Port port)
        {
            // The old Style reference is already gone — look up via tracked handler.
            // Unsubscribe from whatever style was previously wired (if any) by
            // removing our handler from the port's current snapshot, then re-subscribing.
            // Because PropertyChanged fires after the field is set, we track old styles
            // via _portStyleMap so we can unsubscribe.
            if (_portStyles.TryGetValue(port, out var oldStyle) && oldStyle != null)
                oldStyle.PropertyChanged -= OnPortStylePropertyChanged;

            if (port.Style != null)
                port.Style.PropertyChanged += OnPortStylePropertyChanged;

            _portStyles[port] = port.Style;
            InvalidateNodeAdornments(port.Owner);
        }
        else if (e.PropertyName is nameof(Port.AbsolutePosition) or nameof(Port.Label))
        {
            if (sender is Port p)
                InvalidateNodeAdornments(p.Owner);
        }
    }

    private void OnPortStylePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // PortStyle doesn't know its owning port; invalidate all adornments.
        // Style changes are rare so the broader invalidation is acceptable.
        InvalidateAllNodeAdornments();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Node.X) or nameof(Node.Y))
            InvalidateArrange();
        else if (e.PropertyName is nameof(Node.IsSelected))
        {
            if (sender is Node node)
                InvalidateNodeAdornments(node);
        }
        else if (e.PropertyName is nameof(Node.ShowHeader) or nameof(Node.IsCollapsed) or nameof(Node.IsCollapsible))
            InvalidateMeasure();
        else if (e.PropertyName == nameof(Node.PortProvider))
        {
            if (sender is Node node)
            {
                // Detach old provider (tracked before PropertyChanged fired)
                if (_nodeProviders.TryGetValue(node, out var oldProvider))
                {
                    DetachProvider(node, oldProvider);
                    if (oldProvider is ILayoutAwarePortProvider oldLayout)
                        oldLayout.LayoutInvalidated -= OnLayoutAwareProviderInvalidated;
                }

                if (node.PortProvider != null)
                {
                    AttachProvider(node, node.PortProvider);
                    if (node.PortProvider is ILayoutAwarePortProvider newLayout)
                        newLayout.LayoutInvalidated += OnLayoutAwareProviderInvalidated;
                }
            }
            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(Node.Style))
        {
            // NodeStyle is applied at template construction time by DefaultTemplates.
            // When Style changes, rebuild the container to pick up the new values.
            if (sender is Node node && _nodeContainers.TryGetValue(node, out var container))
            {
                var template = DefaultTemplates.ResolveTemplate(node, NodeTemplate);
                if (template != null)
                    container.ContentTemplate = template;
                InvalidateMeasure();
            }
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

            // ShadowPadding only applies to NodePresenter-backed containers (base Node).
            // GroupNode/CommentNode templates use plain Border without shadow margin.
            var pad = node is CommentNode or GroupNode ? 0.0 : ShadowPadding;
            var contentWidth = desired.Width - pad * 2;
            var contentHeight = desired.Height - pad * 2;
            if (contentWidth > 0) node.Width = contentWidth;
            if (contentHeight > 0) node.Height = contentHeight;

            if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
                layoutAware.UpdateLayout(node.Width, node.Height, node.Shape);

            var screenPos = transform.WorldToScreen(new Point(node.X, node.Y));

            container.RenderTransform = new ScaleTransform(ViewportZoom, ViewportZoom);
            container.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
            var adjusted = new Point(screenPos.X - pad * ViewportZoom, screenPos.Y - pad * ViewportZoom);
            container.Arrange(new Rect(adjusted, desired));
        }

        // Overlay fills the entire canvas, renders on top of all node containers
        _overlay.Arrange(new Rect(finalSize));

        return finalSize;
    }

    // --- Shared render cache helpers ---
    // Used by CanvasOverlay and NodeAdornmentLayer. All cache state lives on the canvas
    // so multiple render layers share the same entries without duplicating work.

    internal Pen GetOrCreateStyledPen(IBrush brush, double thickness)
    {
        var key = (brush, thickness);
        if (_styledPenCache.TryGetValue(key, out var pen))
            return pen;

        if (_styledPenCache.Count >= StyledPenCacheMaxEntries)
            _styledPenCache.Clear();

        pen = new Pen(brush, thickness);
        _styledPenCache[key] = pen;
        return pen;
    }

    internal Geometry GetOrCreatePortGeometry(PortShape shape, double bucketedRadius)
    {
        var key = (shape, bucketedRadius);
        if (_portGeometryCache.TryGetValue(key, out var cached))
            return cached;

        if (_portGeometryCache.Count >= PortGeometryCacheMaxEntries)
            _portGeometryCache.Clear();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            switch (shape)
            {
                case PortShape.Diamond:
                    ctx.BeginFigure(new Point(0, -bucketedRadius), true);
                    ctx.LineTo(new Point(bucketedRadius, 0));
                    ctx.LineTo(new Point(0, bucketedRadius));
                    ctx.LineTo(new Point(-bucketedRadius, 0));
                    ctx.EndFigure(true);
                    break;

                case PortShape.Triangle:
                    ctx.BeginFigure(new Point(0, -bucketedRadius), true);
                    ctx.LineTo(new Point(bucketedRadius, bucketedRadius));
                    ctx.LineTo(new Point(-bucketedRadius, bucketedRadius));
                    ctx.EndFigure(true);
                    break;
            }
        }

        _portGeometryCache[key] = geo;
        return geo;
    }

    internal FormattedText GetOrCreateLabel(string text, double bucketedFontSize, IBrush brush)
    {
        var key = (text, bucketedFontSize, brush);
        if (!_labelCache.TryGetValue(key, out var ft))
        {
            if (_labelCache.Count >= LabelCacheMaxEntries)
                _labelCache.Clear();

            ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                bucketedFontSize,
                brush);
            _labelCache[key] = ft;
        }
        return ft;
    }

    internal Pen GetOrCreateSelectedBorderPen(IBrush brush, double thickness)
    {
        if (_cachedSelectedBorderPen == null
            || !ReferenceEquals(_lastSelectedBrush, brush)
            || !_lastSelectedThickness.Equals(thickness))
        {
            _cachedSelectedBorderPen = new Pen(brush, thickness);
            _lastSelectedBrush = brush;
            _lastSelectedThickness = thickness;
        }
        return _cachedSelectedBorderPen!;
    }

    internal Pen GetOrCreateHoveredBorderPen(IBrush brush, double thickness)
    {
        if (_cachedHoveredBorderPen == null
            || !ReferenceEquals(_lastHoveredBrush, brush)
            || !_lastHoveredThickness.Equals(thickness))
        {
            _cachedHoveredBorderPen = new Pen(brush, thickness);
            _lastHoveredBrush = brush;
            _lastHoveredThickness = thickness;
        }
        return _cachedHoveredBorderPen!;
    }

    internal Pen GetOrCreatePortOutlinePen(IBrush brush, double thickness)
    {
        if (_cachedPortOutlinePen == null
            || !ReferenceEquals(_lastPortOutlineBrush, brush)
            || !_lastPortOutlineThickness.Equals(thickness))
        {
            _cachedPortOutlinePen = new Pen(brush, thickness);
            _lastPortOutlineBrush = brush;
            _lastPortOutlineThickness = thickness;
        }
        return _cachedPortOutlinePen!;
    }
}
