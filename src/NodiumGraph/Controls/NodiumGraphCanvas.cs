using System.Collections.Specialized;
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
public class NodiumGraphCanvas : TemplatedControl
{
    static NodiumGraphCanvas()
    {
        ClipToBoundsProperty.OverrideDefaultValue<NodiumGraphCanvas>(true);
        FocusableProperty.OverrideDefaultValue<NodiumGraphCanvas>(true);
        DragDrop.AllowDropProperty.OverrideDefaultValue<NodiumGraphCanvas>(true);
        DragDrop.DropEvent.AddClassHandler<NodiumGraphCanvas>((canvas, e) => canvas.OnDrop(e));
    }

    private const double AutoPanMargin = 40.0;
    private const double AutoPanSpeed = 10.0;

    private readonly Dictionary<Node, ContentControl> _nodeContainers = new();
    private bool _isPanning;
    private bool _isSpaceHeld;
    private Point _panStartScreen;
    private Point _panStartOffset;
    private bool _isDragging;
    private Point _dragStartWorld;
    private Dictionary<Node, Point>? _dragStartPositions;
    private bool _isDrawingConnection;
    private Port? _connectionSourcePort;
    private Point _connectionPreviewEnd;
    private bool _connectionPreviewValid;
    private bool _isCuttingConnections;
    private Point _cuttingStart;
    private Point _cuttingEnd;
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

    internal int NodeContainerCount => _nodeContainers.Count;

    internal bool IsPanning => _isPanning;

    internal bool IsDragging => _isDragging;

    internal bool IsDrawingConnection => _isDrawingConnection;

    internal bool IsCuttingConnections => _isCuttingConnections;

    internal Port? HitTestPort(Point screenPosition)
    {
        if (Graph == null) return null;

        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        var worldPosition = transform.ScreenToWorld(screenPosition);

        foreach (var node in Graph.Nodes)
        {
            if (node.PortProvider == null) continue;
            var port = node.PortProvider.ResolvePort(worldPosition);
            if (port != null) return port;
        }

        return null;
    }

    internal Node? HitTestNode(Point screenPosition)
    {
        // Check containers in reverse order (top-most first, i.e. last-added)
        foreach (var (node, container) in _nodeContainers.Reverse())
        {
            var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
            var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var nodeScreenSize = new Size(
                transform.WorldToScreen(node.Width),
                transform.WorldToScreen(node.Height));
            var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

            if (nodeRect.Contains(screenPosition))
                return node;
        }

        return null;
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
        var hitPort = HitTestPort(position);
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
            var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
            _dragStartWorld = transform.ScreenToWorld(position);
            _dragStartPositions = Graph!.SelectedNodes.ToDictionary(
                n => n,
                n => new Point(n.X, n.Y));
            e.Handled = true;
        }
        else if (!isCtrl)
        {
            ClearSelection();
            // TODO: Start marquee selection in a later task
        }

        Focus();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

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
            var targetPort = HitTestPort(_connectionPreviewEnd);
            _connectionPreviewValid = targetPort != null &&
                targetPort != _connectionSourcePort &&
                (ConnectionValidator?.CanConnect(_connectionSourcePort, targetPort) ?? true);

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

            foreach (var (node, startPos) in _dragStartPositions)
            {
                var newX = startPos.X + delta.X;
                var newY = startPos.Y + delta.Y;

                if (SnapToGrid && GridSize > 0)
                {
                    newX = Math.Round(newX / GridSize) * GridSize;
                    newY = Math.Round(newY / GridSize) * GridSize;
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
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

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
            var targetPort = HitTestPort(position);

            if (targetPort != null && targetPort != _connectionSourcePort)
            {
                var canConnect = ConnectionValidator?.CanConnect(_connectionSourcePort, targetPort) ?? true;
                if (canConnect)
                {
                    ConnectionHandler?.OnConnectionRequested(_connectionSourcePort, targetPort);
                }
            }

            _isDrawingConnection = false;
            _connectionSourcePort = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDragging && _dragStartPositions != null)
        {
            _isDragging = false;

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
            if (_isCuttingConnections)
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);

        if (ShowGrid)
        {
            var gridBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
            GridRenderer.Render(context, Bounds, transform, GridSize, gridBrush);
        }

        if (Graph != null)
        {
            var router = ConnectionRouter;
            var style = DefaultConnectionStyle;
            foreach (var connection in Graph.Connections)
            {
                ConnectionRenderer.Render(context, connection, router, style, transform);
            }

            // Render port visuals
            var portBrush = new SolidColorBrush(Color.FromRgb(160, 160, 170));
            const double portRadius = 4.0;

            foreach (var node in Graph.Nodes)
            {
                if (node.PortProvider == null) continue;
                foreach (var port in node.PortProvider.Ports)
                {
                    var screenPos = transform.WorldToScreen(port.AbsolutePosition);
                    var scaledRadius = portRadius * ViewportZoom;

                    // Default: filled circle
                    context.DrawEllipse(portBrush, new Pen(Brushes.White, 1),
                        screenPos, scaledRadius, scaledRadius);
                }
            }
        }

        if (_isDrawingConnection && _connectionSourcePort != null)
        {
            var startScreen = transform.WorldToScreen(_connectionSourcePort.AbsolutePosition);
            var previewBrush = _connectionPreviewValid
                ? Brushes.Green
                : Brushes.Gray;
            var pen = new Pen(previewBrush, 2.0, new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawLine(pen, startScreen, _connectionPreviewEnd);
        }

        if (_isCuttingConnections)
        {
            var cuttingPen = new Pen(Brushes.Red, 2.0, new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawLine(cuttingPen, _cuttingStart, _cuttingEnd);
        }

        if (ShowMinimap && Graph != null)
        {
            MinimapRenderer.Render(context, Bounds, Graph, transform, MinimapPosition);
        }
    }

    private bool CuttingLineIntersectsGeometry(
        Point lineStart, Point lineEnd, Connection connection, ViewportTransform transform)
    {
        var routePoints = ConnectionRouter.Route(connection.SourcePort, connection.TargetPort);
        var screenPoints = routePoints.Select(transform.WorldToScreen).ToList();

        // For bezier (4 points), sample the curve at intervals for approximate intersection
        if (screenPoints.Count == 4)
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

        return false;
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
                node.PropertyChanged -= OnNodePropertyChanged;
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
                LogicalChildren.Remove(container);
                VisualChildren.Remove(container);
            }

            _nodeContainers.Clear();
        }
    }

    private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void AddNodeContainer(Node node)
    {
        var template = DefaultTemplates.ResolveTemplate(node, NodeTemplate);
        var container = new ContentControl
        {
            DataContext = node,
            Content = node,
            ContentTemplate = template
        };
        _nodeContainers[node] = container;
        LogicalChildren.Add(container);
        VisualChildren.Add(container);

        node.PropertyChanged += OnNodePropertyChanged;

        InvalidateMeasure();
    }

    private void RemoveNodeContainer(Node node)
    {
        if (_nodeContainers.TryGetValue(node, out var container))
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            LogicalChildren.Remove(container);
            VisualChildren.Remove(container);
            _nodeContainers.Remove(node);
            InvalidateMeasure();
        }
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Node.X) or nameof(Node.Y))
            InvalidateArrange();
        else if (e.PropertyName is nameof(Node.IsSelected))
            InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var (_, container) in _nodeContainers)
        {
            container.Measure(Size.Infinity);
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);

        foreach (var (node, container) in _nodeContainers)
        {
            var desired = container.DesiredSize;
            if (desired.Width > 0) node.Width = desired.Width;
            if (desired.Height > 0) node.Height = desired.Height;

            var screenPos = transform.WorldToScreen(new Point(node.X, node.Y));

            container.RenderTransform = new ScaleTransform(ViewportZoom, ViewportZoom);
            container.Arrange(new Rect(screenPos, desired));
        }

        return finalSize;
    }
}
