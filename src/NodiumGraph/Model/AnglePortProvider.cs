using System.ComponentModel;
using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Port provider that positions ports on the node boundary based on their Angle property.
/// Uses INodeShape to compute boundary points.
/// </summary>
public class AnglePortProvider : ILayoutAwarePortProvider
{
    private const double DefaultHitRadius = 20.0;
    private static readonly INodeShape DefaultShape = new RectangleShape();

    private readonly List<Port> _ports = new();
    private readonly double _hitRadiusSq;
    private double _lastWidth;
    private double _lastHeight;
    private INodeShape _lastShape = DefaultShape;

    public IReadOnlyList<Port> Ports => _ports.AsReadOnly();

    public event Action? LayoutInvalidated;

    public AnglePortProvider(double hitRadius = DefaultHitRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitRadius);
        _hitRadiusSq = hitRadius * hitRadius;
    }

    /// <summary>
    /// Adds a port to this provider and subscribes to its PropertyChanged for angle tracking.
    /// </summary>
    public void AddPort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        _ports.Add(port);
        port.PropertyChanged += OnPortPropertyChanged;
        RecomputePortPosition(port);
    }

    /// <summary>
    /// Removes a port from this provider and unsubscribes from its PropertyChanged.
    /// </summary>
    public bool RemovePort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (!_ports.Remove(port))
            return false;
        port.PropertyChanged -= OnPortPropertyChanged;
        return true;
    }

    /// <summary>
    /// Distributes all ports evenly around the boundary at equal angular intervals.
    /// </summary>
    public void DistributeEvenly()
    {
        if (_ports.Count == 0) return;

        var step = 360.0 / _ports.Count;
        for (var i = 0; i < _ports.Count; i++)
        {
            _ports[i].Angle = i * step;
        }
        // Positions will be recomputed via PropertyChanged on each Angle set
    }

    public void UpdateLayout(double width, double height, INodeShape? shape)
    {
        _lastWidth = width;
        _lastHeight = height;
        _lastShape = shape ?? DefaultShape;

        RecomputeAllPositions();
    }

    public Port? ResolvePort(Point position)
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

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Port.Angle) && sender is Port port)
        {
            RecomputePortPosition(port);
            LayoutInvalidated?.Invoke();
        }
    }

    private void RecomputeAllPositions()
    {
        foreach (var port in _ports)
        {
            RecomputePortPosition(port);
        }

        LayoutInvalidated?.Invoke();
    }

    private void RecomputePortPosition(Port port)
    {
        if (_lastWidth <= 0 || _lastHeight <= 0) return;

        // GetBoundaryPoint returns center-relative coordinates
        var centerRelative = _lastShape.GetBoundaryPoint(port.Angle, _lastWidth, _lastHeight);

        // Convert to top-left-relative for Port.Position
        port.Position = new Point(
            centerRelative.X + _lastWidth / 2.0,
            centerRelative.Y + _lastHeight / 2.0);
    }
}
