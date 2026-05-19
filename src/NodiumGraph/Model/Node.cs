using Avalonia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NodiumGraph.Model;

/// <summary>
/// A visual node in the graph. Subclass to attach domain data.
/// Width and Height are set internally by the canvas after measure.
/// </summary>
public class Node : INotifyPropertyChanged, IGraphElement
{
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private string _title;
    private bool _isSelected;
    private bool _showHeader = true;
    private bool _isCollapsed;
    private bool _isCollapsible;
    private NodeStyle? _style;
    private INodeShape _shape = new RectangleShape();
    private IPortProvider? _portProvider;
    private bool _portProviderExplicit;

    public Node()
    {
        _title = GetType().Name;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public double Width
    {
        get => _width;
        internal set => SetField(ref _width, value);
    }

    public double Height
    {
        get => _height;
        internal set => SetField(ref _height, value);
    }

    public IPortProvider? PortProvider
    {
        get
        {
            EnsureMaterialized();
            return _portProvider;
        }
        set
        {
            _portProviderExplicit = true;     // any assignment (including null) suppresses registry defaults
            SetField(ref _portProvider, value);
        }
    }

    /// <summary>
    /// All ports owned by this node. Equivalent to <c>PortProvider?.Ports</c> — but also
    /// triggers lazy materialization from <see cref="NodePortRegistry"/> on first access
    /// when the consumer has not assigned a provider in code.
    /// </summary>
    public IReadOnlyList<Port> Ports
    {
        get
        {
            EnsureMaterialized();
            return _portProvider?.Ports ?? Array.Empty<Port>();
        }
    }

    /// <summary>
    /// Defines the geometric boundary shape used for angle-based port positioning.
    /// Defaults to RectangleShape. Throws ArgumentNullException if assigned null.
    /// </summary>
    public INodeShape Shape
    {
        get => _shape;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetField(ref _shape, value);
        }
    }

    /// <summary>
    /// Returns the node-local boundary point for the anchor under the current Width/Height/Shape.
    /// </summary>
    public Point GetEdgePoint(PortAnchor anchor) =>
        Shape.GetEdgePoint(anchor, Width, Height);

    /// <summary>
    /// Returns the outward unit normal at the boundary point addressed by the anchor.
    /// </summary>
    public Vector GetEdgeOutwardNormal(PortAnchor anchor) =>
        Shape.GetEdgeOutwardNormal(anchor, Width, Height);

    /// <summary>
    /// Returns the canonical anchor for the given node-local boundary point.
    /// </summary>
    public PortAnchor InferAnchor(Point boundaryLocal) =>
        Shape.InferAnchor(boundaryLocal, Width, Height);

    /// <summary>
    /// Returns the nearest point on the shape boundary, in center-relative coordinates.
    /// </summary>
    public Point GetNearestBoundaryPoint(Point centerRelative) =>
        Shape.GetNearestBoundaryPoint(centerRelative, Width, Height);

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetField(ref _isSelected, value);
    }

    /// <summary>
    /// Controls whether the default template renders the header bar.
    /// When false, the header is hidden and node height shrinks naturally.
    /// Title remains unchanged regardless of this value.
    /// </summary>
    public bool ShowHeader
    {
        get => _showHeader;
        set => SetField(ref _showHeader, value);
    }

    /// <summary>
    /// Controls whether the node shows a collapse/expand toggle.
    /// When true, the default template renders a clickable arrow at the bottom
    /// that toggles <see cref="IsCollapsed"/>.
    /// </summary>
    public bool IsCollapsible
    {
        get => _isCollapsible;
        set => SetField(ref _isCollapsible, value);
    }

    /// <summary>
    /// Controls whether the node is collapsed. When true:
    /// - Behavioral (canvas-enforced): ports are hidden, not hit-testable, new connections blocked.
    /// - Visual (default template): body section hidden, height shrinks naturally.
    /// No built-in collapse button — consumer triggers via this setter.
    /// </summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetField(ref _isCollapsed, value);
    }

    /// <summary>
    /// Per-instance visual overrides. Null properties fall through to theme, then default.
    /// </summary>
    /// <remarks>
    /// Style properties are applied when the node's DataTemplate is first created.
    /// Changing style properties at runtime will not automatically update the node's
    /// visuals — the consumer must force a template rebuild (e.g., by removing and
    /// re-adding the node) for changes to take effect. This is a known limitation
    /// of the FuncDataTemplate approach.
    /// </remarks>
    public NodeStyle? Style
    {
        get => _style;
        set => SetField(ref _style, value);
    }

    private void EnsureMaterialized()
    {
        if (_portProviderExplicit) return;
        if (!NodePortRegistry.TryGet(GetType(), out var specs)) return;

        var ports = specs.Select(s => new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction ?? 0.5))
        {
            Label = s.Label,
            MaxConnections = s.MaxConnections,
            DataType = s.DataType,
        }).ToList();

        PortProvider = new FixedPortProvider(ports);   // routes through setter → sets sentinel, fires PropertyChanged
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
