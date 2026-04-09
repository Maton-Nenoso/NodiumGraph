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

    private readonly Node _owner;
    private readonly List<Port> _ports = new();
    private readonly double _reuseThresholdSq;
    private readonly double _maxDistanceSq;

    public IReadOnlyList<Port> Ports => _ports.AsReadOnly();

    public DynamicPortProvider(Node owner, double reuseThreshold = DefaultReuseThreshold, double maxDistance = DefaultMaxDistance)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reuseThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDistance);
        _owner = owner;
        _reuseThresholdSq = reuseThreshold * reuseThreshold;
        _maxDistanceSq = maxDistance * maxDistance;
    }

    public Port? ResolvePort(Point position)
    {
        if (_owner.Width <= 0 || _owner.Height <= 0)
            return null;

        var boundary = FindNearestBoundaryPoint(position);
        if (boundary is null)
            return null;

        foreach (var existing in _ports)
        {
            var abs = existing.AbsolutePosition;
            var dx = abs.X - boundary.Value.X;
            var dy = abs.Y - boundary.Value.Y;
            if (dx * dx + dy * dy < _reuseThresholdSq)
                return existing;
        }

        var relative = new Point(boundary.Value.X - _owner.X, boundary.Value.Y - _owner.Y);
        var port = new Port(_owner, relative);
        _ports.Add(port);
        return port;
    }

    private Point? FindNearestBoundaryPoint(Point position)
    {
        var left = _owner.X;
        var top = _owner.Y;
        var right = _owner.X + _owner.Width;
        var bottom = _owner.Y + _owner.Height;

        var clampedX = Math.Clamp(position.X, left, right);
        var clampedY = Math.Clamp(position.Y, top, bottom);

        var distLeft = Math.Abs(clampedX - left);
        var distRight = Math.Abs(clampedX - right);
        var distTop = Math.Abs(clampedY - top);
        var distBottom = Math.Abs(clampedY - bottom);

        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        Point boundaryPoint;
        if (minDist == distLeft)
            boundaryPoint = new Point(left, clampedY);
        else if (minDist == distRight)
            boundaryPoint = new Point(right, clampedY);
        else if (minDist == distTop)
            boundaryPoint = new Point(clampedX, top);
        else
            boundaryPoint = new Point(clampedX, bottom);

        var dx = position.X - boundaryPoint.X;
        var dy = position.Y - boundaryPoint.Y;
        if (dx * dx + dy * dy > _maxDistanceSq)
            return null;

        return boundaryPoint;
    }
}
