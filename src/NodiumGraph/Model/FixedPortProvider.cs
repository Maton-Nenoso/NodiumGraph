using Avalonia;

namespace NodiumGraph.Model;

public class FixedPortProvider : IPortProvider
{
    private const double DefaultHitRadius = 20.0;
    private readonly double _hitRadius;

    public IReadOnlyList<Port> Ports { get; }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius)
    {
        Ports = ports.ToList().AsReadOnly();
        _hitRadius = hitRadius;
    }

    public Port? ResolvePort(Point position)
    {
        Port? closest = null;
        var closestDistance = double.MaxValue;

        foreach (var port in Ports)
        {
            var abs = port.AbsolutePosition;
            var dx = abs.X - position.X;
            var dy = abs.Y - position.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < _hitRadius && distance < closestDistance)
            {
                closest = port;
                closestDistance = distance;
            }
        }

        return closest;
    }
}
