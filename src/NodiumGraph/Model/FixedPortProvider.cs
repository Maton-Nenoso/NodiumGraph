using Avalonia;
using System;
using System.Collections.Generic;

namespace NodiumGraph.Model;

/// <summary>
/// Port provider with a fixed set of declared ports. Resolves the nearest port within a hit radius.
/// </summary>
public class FixedPortProvider : IPortProvider
{
    private const double DefaultHitRadius = 20.0;

    private readonly List<Port> _ports = new();
    private readonly double _hitRadiusSq;

    public IReadOnlyList<Port> Ports { get; }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;

    public FixedPortProvider(double hitRadius = DefaultHitRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitRadius);
        _hitRadiusSq = hitRadius * hitRadius;
        Ports = _ports.AsReadOnly();
    }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius) : this(hitRadius)
    {
        ArgumentNullException.ThrowIfNull(ports);
        foreach (var port in ports)
        {
            ArgumentNullException.ThrowIfNull(port, nameof(ports));
            _ports.Add(port);
        }
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
}
