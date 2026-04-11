using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Port provider that creates ports dynamically at the nearest node boundary point.
/// Reuses existing ports within a distance threshold.
/// </summary>
public class DynamicPortProvider : IPortProvider
{
    private const double DefaultReuseThreshold = 15.0;
    private const double DefaultMaxDistance = 50.0;
    private static readonly INodeShape DefaultShape = new RectangleShape();

    private readonly Node _owner;
    private readonly List<Port> _ports = new();
    private readonly double _reuseThresholdSq;
    private readonly double _maxDistanceSq;
    private Port? _lastCreated;

    public IReadOnlyList<Port> Ports { get; }

    /// <summary>
    /// When true, ports with no connections are automatically removed when
    /// <see cref="NotifyDisconnected"/> is called.
    /// </summary>
    public bool AutoPruneOnDisconnect { get; set; }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;

    public DynamicPortProvider(Node owner, double reuseThreshold = DefaultReuseThreshold, double maxDistance = DefaultMaxDistance)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reuseThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDistance);
        _owner = owner;
        _reuseThresholdSq = reuseThreshold * reuseThreshold;
        _maxDistanceSq = maxDistance * maxDistance;
        Ports = _ports.AsReadOnly();
    }

    public Port? ResolvePort(Point position, bool preview)
    {
        if (_owner.Width <= 0 || _owner.Height <= 0)
            return null;

        var shape = _owner.Shape ?? DefaultShape;
        var boundary = FindNearestBoundaryPoint(position, shape);
        if (boundary is null)
            return null;

        // Check for existing port to reuse
        foreach (var existing in _ports)
        {
            var abs = existing.AbsolutePosition;
            var dx = abs.X - boundary.Value.X;
            var dy = abs.Y - boundary.Value.Y;
            if (dx * dx + dy * dy < _reuseThresholdSq)
            {
                if (!preview) _lastCreated = null; // Reusing — clear last-created tracking
                return existing;
            }
        }

        if (preview) return null; // Preview never creates

        // Create new port
        var relative = new Point(boundary.Value.X - _owner.X, boundary.Value.Y - _owner.Y);
        var port = new Port(_owner, relative);
        _ports.Add(port);
        _lastCreated = port;
        PortAdded?.Invoke(port);
        return port;
    }

    /// <summary>
    /// Removes the last port created by <see cref="ResolvePort"/> (if any).
    /// Call on drag cancel or failed connection commit to roll back the tentative port.
    /// No-op if the last resolve reused an existing port or no port was created.
    /// </summary>
    public void CancelResolve()
    {
        if (_lastCreated == null) return;
        if (_ports.Remove(_lastCreated))
        {
            _lastCreated.Detach();
            PortRemoved?.Invoke(_lastCreated);
        }
        _lastCreated = null;
    }

    /// <summary>
    /// Called by the consumer after a connection involving <paramref name="port"/> is removed.
    /// When <see cref="AutoPruneOnDisconnect"/> is true and the port has no remaining connections,
    /// the port is removed from this provider.
    /// </summary>
    public void NotifyDisconnected(Port port, Graph graph)
    {
        if (!AutoPruneOnDisconnect) return;
        if (!_ports.Contains(port)) return;

        var hasConnections = false;
        foreach (var conn in graph.Connections)
        {
            if (conn.SourcePort == port || conn.TargetPort == port)
            {
                hasConnections = true;
                break;
            }
        }

        if (!hasConnections)
        {
            _ports.Remove(port);
            port.Detach();
            PortRemoved?.Invoke(port);
        }
    }

    /// <summary>
    /// Removes all ports that have no connections in <paramref name="graph"/>.
    /// </summary>
    public void PruneUnconnected(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        for (var i = _ports.Count - 1; i >= 0; i--)
        {
            var port = _ports[i];
            var connected = false;
            foreach (var conn in graph.Connections)
            {
                if (conn.SourcePort == port || conn.TargetPort == port)
                {
                    connected = true;
                    break;
                }
            }
            if (!connected)
            {
                _ports.RemoveAt(i);
                port.Detach();
                PortRemoved?.Invoke(port);
            }
        }
    }

    private Point? FindNearestBoundaryPoint(Point position, INodeShape shape)
    {
        var centerX = _owner.X + _owner.Width / 2.0;
        var centerY = _owner.Y + _owner.Height / 2.0;
        var centerRel = new Point(position.X - centerX, position.Y - centerY);

        var boundaryCenter = shape.GetNearestBoundaryPoint(centerRel, _owner.Width, _owner.Height);
        var worldBoundary = new Point(boundaryCenter.X + centerX, boundaryCenter.Y + centerY);

        var dx = position.X - worldBoundary.X;
        var dy = position.Y - worldBoundary.Y;
        if (dx * dx + dy * dy > _maxDistanceSq)
            return null;

        return worldBoundary;
    }
}
