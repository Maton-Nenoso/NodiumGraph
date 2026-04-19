using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Resolves the outward emission direction of a port from which edge of its owner
/// node it sits closest to. Returns one of the four cardinal unit vectors.
/// </summary>
internal static class PortEmissionDirection
{
    public static Vector Resolve(Port port)
    {
        var owner = port.Owner;
        var px = port.Position.X;
        var py = port.Position.Y;

        var leftDist = px;
        var rightDist = owner.Width - px;
        var topDist = py;
        var bottomDist = owner.Height - py;

        // Pick the axis with the smallest edge distance, then the nearer side on that axis.
        // Negative distances (port declared outside its owner) naturally win because they're
        // the smallest — the emission vector then points toward the side the port is beyond.
        // Ties break horizontal-first to preserve the default for corner / interior / zero-size ports.
        var minHorizontal = Math.Min(leftDist, rightDist);
        var minVertical = Math.Min(topDist, bottomDist);

        if (minHorizontal <= minVertical)
        {
            return leftDist <= rightDist ? new Vector(-1, 0) : new Vector(1, 0);
        }

        return topDist <= bottomDist ? new Vector(0, -1) : new Vector(0, 1);
    }
}
