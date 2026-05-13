using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Tests.Helpers;

/// <summary>
/// Test helpers for constructing ports via anchor inference. If the owner has zero Width/Height,
/// the helper grows it to encompass the requested point so InferAnchor can map back to (x, y).
/// </summary>
internal static class TestNodes
{
    public static Port PortAt(Node owner, double x, double y, string name = "", PortFlow flow = PortFlow.Input)
    {
        // Ensure owner has dimensions that can contain (x, y) for anchor inference.
        // We only grow the size — never shrink — so tests that explicitly set size keep theirs.
        // Use max(x, 1) to avoid zero dimensions when x or y is 0 (boundary points).
        var needW = Math.Max(x, 1.0);
        var needH = Math.Max(y, 1.0);
        if (owner.Width < needW)  owner.Width  = needW;
        if (owner.Height < needH) owner.Height = needH;

        var anchor = owner.InferAnchor(new Point(x, y));
        return new Port(owner, name, flow, anchor);
    }

    public static Port PortAt(Node owner, Point local, string name = "", PortFlow flow = PortFlow.Input)
        => PortAt(owner, local.X, local.Y, name, flow);
}
