using Avalonia;

namespace NodiumGraph.Model;

public class DynamicPortProvider : IPortProvider
{
    private const double DefaultReuseThreshold = 15.0;
    private const double DefaultMaxDistance = 50.0;

    private readonly Node _owner;
    private readonly List<Port> _ports = new();
    private readonly double _reuseThreshold;
    private readonly double _maxDistance;

    public IReadOnlyList<Port> Ports => _ports;

    public DynamicPortProvider(Node owner, double reuseThreshold = DefaultReuseThreshold, double maxDistance = DefaultMaxDistance)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _reuseThreshold = reuseThreshold;
        _maxDistance = maxDistance;
    }

    public Port? ResolvePort(Point position)
    {
        var boundary = FindNearestBoundaryPoint(position);
        if (boundary is null)
            return null;

        foreach (var existing in _ports)
        {
            var abs = existing.AbsolutePosition;
            var dx = abs.X - boundary.Value.X;
            var dy = abs.Y - boundary.Value.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < _reuseThreshold)
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
        if (Math.Sqrt(dx * dx + dy * dy) > _maxDistance)
            return null;

        return boundaryPoint;
    }
}
