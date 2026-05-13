using Avalonia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NodiumGraph.Model;

/// <summary>
/// A connection endpoint on a node. Position is derived from the immutable Anchor
/// and the owner's current geometry (Width, Height, Shape).
/// </summary>
public class Port : INotifyPropertyChanged
{
    private PortStyle? _style;
    private string? _label;
    private uint? _maxConnections;
    private object? _dataType;

    private Point _cachedPosition;
    private Point _cachedAbsolutePosition;
    private bool _positionDirty = true;
    private bool _absolutePositionDirty = true;
    private bool _isDetached;

    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }
    public PortAnchor Anchor { get; }

    public Port(Node owner, string name, PortFlow flow, PortAnchor anchor)
    {
        Owner  = owner ?? throw new ArgumentNullException(nameof(owner));
        Name   = name  ?? throw new ArgumentNullException(nameof(name));
        Flow   = flow;
        Anchor = anchor;
        Owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    /// <summary>
    /// Node-local boundary point derived from Anchor + Owner geometry. Cached; invalidates on owner Width/Height/Shape change.
    /// </summary>
    public Point Position
    {
        get
        {
            if (_positionDirty)
            {
                _cachedPosition = Owner.GetEdgePoint(Anchor);
                _positionDirty = false;
            }
            return _cachedPosition;
        }
    }

    /// <summary>
    /// World-space position = Owner.X/Y + Position. Cached; invalidates on owner X/Y/Width/Height/Shape change.
    /// </summary>
    public Point AbsolutePosition
    {
        get
        {
            if (_absolutePositionDirty)
            {
                var local = Position;
                _cachedAbsolutePosition = new Point(Owner.X + local.X, Owner.Y + local.Y);
                _absolutePositionDirty = false;
            }
            return _cachedAbsolutePosition;
        }
    }

    /// <summary>
    /// Outward unit normal at the port's boundary point — used by routers for connection emission direction.
    /// </summary>
    public Vector EmissionDirection => Owner.GetEdgeOutwardNormal(Anchor);

    /// <summary>
    /// Per-instance visual overrides. Null properties fall through to theme, then default.
    /// </summary>
    public PortStyle? Style
    {
        get => _style;
        set => SetField(ref _style, value);
    }

    /// <summary>
    /// Optional text label displayed next to the port.
    /// When null, no label is rendered.
    /// </summary>
    public string? Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }

    /// <summary>
    /// Maximum number of simultaneous connections allowed on this port.
    /// Null means unlimited. This is metadata for connection validators — the library never enforces it.
    /// </summary>
    public uint? MaxConnections
    {
        get => _maxConnections;
        set => SetField(ref _maxConnections, value);
    }

    /// <summary>
    /// Opaque type token consumed by IConnectionValidator. The library never inspects this value
    /// beyond equality comparison in the default validator. Prefer reference-typed tokens
    /// (e.g. <see cref="string"/>, <see cref="System.Type"/>, class-based records) to avoid
    /// boxing on every validator call during connection drags.
    /// </summary>
    public object? DataType
    {
        get => _dataType;
        set => SetField(ref _dataType, value);
    }

    /// <summary>
    /// Unsubscribes from the owner node's PropertyChanged event.
    /// Call when removing this port from its provider to prevent memory leaks.
    /// </summary>
    internal void Detach()
    {
        if (_isDetached) return;
        Owner.PropertyChanged -= OnOwnerPropertyChanged;
        _isDetached = true;
    }

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (name is nameof(Node.Width) or nameof(Node.Height) or nameof(Node.Shape))
        {
            _positionDirty = true;
            _absolutePositionDirty = true;
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(AbsolutePosition));
            OnPropertyChanged(nameof(EmissionDirection));
        }
        else if (name is nameof(Node.X) or nameof(Node.Y))
        {
            _absolutePositionDirty = true;
            OnPropertyChanged(nameof(AbsolutePosition));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
