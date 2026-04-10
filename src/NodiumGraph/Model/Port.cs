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
    private double _angle;
    private string? _label;
    private PortLabelPlacement? _labelPlacement;

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

    /// <summary>
    /// Angle in degrees for angle-based positioning. 0 = top, clockwise (90 = right, 180 = bottom, 270 = left).
    /// Used by AnglePortProvider to compute boundary position.
    /// </summary>
    public double Angle
    {
        get => _angle;
        set => SetField(ref _angle, value);
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
    /// Controls where the label is placed relative to the port.
    /// When null, placement is auto-determined from the port's angle:
    /// top (315-45) -> Below, right (45-135) -> Left, bottom (135-225) -> Above, left (225-315) -> Right.
    /// </summary>
    public PortLabelPlacement? LabelPlacement
    {
        get => _labelPlacement;
        set => SetField(ref _labelPlacement, value);
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
