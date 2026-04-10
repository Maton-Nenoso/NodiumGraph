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

    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }

    public Point Position
    {
        get => _position;
        internal set => SetField(ref _position, value);
    }

    /// <summary>
    /// World-space position computed from the owner node's location and this port's relative position.
    /// </summary>
    public Point AbsolutePosition => new(Owner.X + Position.X, Owner.Y + Position.Y);

    /// <summary>
    /// Per-instance visual overrides. Null properties fall through to theme, then default.
    /// </summary>
    public PortStyle? Style
    {
        get => _style;
        set => SetField(ref _style, value);
    }

    public Port(Node owner, string name, PortFlow flow, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Flow = flow;
        _position = position;
    }

    public Port(Node owner, Point position) : this(owner, string.Empty, PortFlow.Input, position)
    {
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
