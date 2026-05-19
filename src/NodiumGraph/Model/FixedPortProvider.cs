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

        // Initial layout pass: distribute auto ports on each edge that has any.
        // Runs after all ports are buffered so per-edge auto count is correct.
        // Does NOT fire PortAdded — these are initial members, not runtime additions.
        var edgesWithAuto = new HashSet<PortEdge>();
        foreach (var p in _ports)
            if (p.IsAutoFraction) edgesWithAuto.Add(p.Anchor.Edge);
        foreach (var edge in edgesWithAuto)
            DistributeAuto(_ports, edge);
    }

    public void AddPort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        _ports.Add(port);
        if (port.IsAutoFraction)
            DistributeAuto(_ports, port.Anchor.Edge);
        PortAdded?.Invoke(port);
    }

    public bool RemovePort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (!_ports.Remove(port)) return false;
        var wasAuto = port.IsAutoFraction;
        var removedEdge = port.Anchor.Edge;
        port.Detach();
        if (wasAuto)
            DistributeAuto(_ports, removedEdge);
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

    /// <summary>
    /// For every auto port on <paramref name="edge"/>, set its Anchor to
    /// <c>(edge, (i + 1) / (N_auto + 1))</c> where <c>i</c> is the port's index among auto
    /// ports on that edge (insertion order in <paramref name="ports"/>) and <c>N_auto</c> is
    /// the total count of auto ports on the edge. Pinned ports are ignored.
    /// Idempotent: <c>SetAnchor</c>'s structural-equality short-circuit means a
    /// no-op pass fires zero INPC events.
    /// </summary>
    private static void DistributeAuto(IReadOnlyList<Port> ports, PortEdge edge)
    {
        int autoCount = 0;
        foreach (var p in ports)
            if (p.Anchor.Edge == edge && p.IsAutoFraction) autoCount++;

        if (autoCount == 0) return;

        int autoIndex = 0;
        foreach (var p in ports)
        {
            if (p.Anchor.Edge != edge || !p.IsAutoFraction) continue;
            double f = (autoIndex + 1.0) / (autoCount + 1.0);
            p.SetAnchor(new PortAnchor(edge, f));
            autoIndex++;
        }
    }
}
