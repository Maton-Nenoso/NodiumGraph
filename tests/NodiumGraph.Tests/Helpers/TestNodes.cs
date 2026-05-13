using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Tests.Helpers;

/// <summary>
/// Test helpers for constructing ports. Centralizes port construction so the
/// underlying implementation (point-based today, anchor-based after Task 7)
/// can shift without per-test churn.
/// </summary>
internal static class TestNodes
{
    public static Port PortAt(Node owner, double x, double y, string name = "", PortFlow flow = PortFlow.Input)
        => new Port(owner, name, flow, new Point(x, y));

    public static Port PortAt(Node owner, Point local, string name = "", PortFlow flow = PortFlow.Input)
        => PortAt(owner, local.X, local.Y, name, flow);
}
