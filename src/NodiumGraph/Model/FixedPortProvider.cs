using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Port provider with a fixed set of declared ports. Resolves the nearest port within a hit radius.
/// </summary>
public class FixedPortProvider : IPortProvider
{
    private const double DefaultHitRadius = 20.0;
    private readonly double _hitRadiusSq;

    public IReadOnlyList<Port> Ports { get; }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitRadius);
        Ports = ports.ToList().AsReadOnly();
        _hitRadiusSq = hitRadius * hitRadius;
    }

    public Port? ResolvePort(Point position)
    {
        Port? closest = null;
        var closestDistSq = double.MaxValue;

        foreach (var port in Ports)
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
}
