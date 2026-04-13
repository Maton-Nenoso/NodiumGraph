using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// A connection endpoint on a node. Position is relative to the node's top-left corner.
/// </summary>
public class Port : INotifyPropertyChanged
{
    private Point _position;
    private PortStyle? _style;
    private string? _label;
    private uint? _maxConnections;
    private object? _dataType;
    private Point _cachedAbsolutePosition;
    private bool _absolutePositionDirty = true;
    private bool _isDetached;

    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }

    public Point Position
    {
        get => _position;
        internal set
        {
            if (SetField(ref _position, value))
            {
                _absolutePositionDirty = true;
                OnPropertyChanged(nameof(AbsolutePosition));
            }
        }
    }

    /// <summary>
    /// World-space position computed from the owner node's location and this port's relative position.
    /// Cached and invalidated when the owner node moves or the port's relative position changes.
    /// </summary>
    public Point AbsolutePosition
    {
        get
        {
            if (_absolutePositionDirty)
            {
                _cachedAbsolutePosition = new Point(Owner.X + Position.X, Owner.Y + Position.Y);
                _absolutePositionDirty = false;
            }
            return _cachedAbsolutePosition;
        }
    }

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

    public Port(Node owner, string name, PortFlow flow, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Flow = flow;
        _position = position;
        Owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public Port(Node owner, Point position) : this(owner, string.Empty, PortFlow.Input, position) { }

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
        if (e.PropertyName is nameof(Node.X) or nameof(Node.Y))
        {
            _absolutePositionDirty = true;
            OnPropertyChanged(nameof(AbsolutePosition));
        }
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
