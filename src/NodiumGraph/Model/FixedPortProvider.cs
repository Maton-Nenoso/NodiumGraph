using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Port provider with a fixed set of declared ports. Resolves the nearest port within a hit radius.
/// When constructed with <c>layoutAware: true</c>, <see cref="UpdateLayout"/> snaps each port's
/// position to the nearest boundary point of the node shape.
/// </summary>
public class FixedPortProvider : ILayoutAwarePortProvider
{
    private const double DefaultHitRadius = 20.0;
    private static readonly INodeShape DefaultShape = new RectangleShape();

    private readonly List<Port> _ports = new();
    private readonly double _hitRadiusSq;
    private readonly bool _layoutAware;
    private double _lastWidth;
    private double _lastHeight;
    private INodeShape _lastShape = DefaultShape;

    public IReadOnlyList<Port> Ports { get; }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;
    public event Action? LayoutInvalidated;

    public FixedPortProvider(double hitRadius = DefaultHitRadius, bool layoutAware = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitRadius);
        _hitRadiusSq = hitRadius * hitRadius;
        _layoutAware = layoutAware;
        Ports = _ports.AsReadOnly();
    }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius, bool layoutAware = false)
        : this(hitRadius, layoutAware)
    {
        ArgumentNullException.ThrowIfNull(ports);
        _ports.AddRange(ports);
    }

    public void AddPort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        _ports.Add(port);
        PortAdded?.Invoke(port);
    }

    public bool RemovePort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (!_ports.Remove(port)) return false;
        port.Detach();
        PortRemoved?.Invoke(port);
        return true;
    }

    public Port? ResolvePort(Point position, bool preview)
    {
        Port? closest = null;
        var closestDistSq = double.MaxValue;

        foreach (var port in _ports)
        {
            var abs = port.AbsolutePosition;
            var dx = abs.X - position.X;
            var dy = abs.Y - position.Y;
            var distSq = dx * dx + dy * dy;

            if (distSq < _hitRadiusSq && distSq < closestDistSq)
            {
                closest = port;
                closestDistSq = distSq;
            }
        }

        return closest;
    }

    public void CancelResolve() { }

    public void UpdateLayout(double width, double height, INodeShape? shape)
    {
        _lastWidth = width;
        _lastHeight = height;
        _lastShape = shape ?? DefaultShape;

        if (!_layoutAware) return;

        foreach (var port in _ports)
        {
            var centerRel = new Point(port.Position.X - _lastWidth / 2.0, port.Position.Y - _lastHeight / 2.0);
            var boundary = _lastShape.GetNearestBoundaryPoint(centerRel, _lastWidth, _lastHeight);
            port.Position = new Point(boundary.X + _lastWidth / 2.0, boundary.Y + _lastHeight / 2.0);
        }

        LayoutInvalidated?.Invoke();
    }
}
