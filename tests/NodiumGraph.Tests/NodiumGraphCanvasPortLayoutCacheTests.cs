using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

using NodiumGraph.Tests.Helpers;
namespace NodiumGraph.Tests;

/// <summary>
/// Pins the cache-invalidation delta required by PortLayout: when an auto port is
/// added to an edge that already has connected auto ports, the existing ports move
/// (via DistributeAuto) while their owning node is stationary. The canvas must drop
/// the cached connection geometry for that node — but only for that node, not all
/// connections.
/// </summary>
public class NodiumGraphCanvasPortLayoutCacheTests
{
    private const int CanvasWidth = 800;
    private const int CanvasHeight = 600;

    [AvaloniaFact]
    public void AutoPort_AddOnSameEdge_InvalidatesAffectedConnectionOnly_LeavesUnrelatedCached()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();

        // Pair A↔B: auto ports on the moving side.
        var nodeA = new Node { X = 100, Y = 100 };
        var nodeB = new Node { X = 400, Y = 100 };
        var aPortOut = new Port(nodeA, "out",  PortFlow.Output, PortEdge.Right);
        var bPortIn  = new Port(nodeB, "in",   PortFlow.Input,  PortEdge.Left);
        nodeA.PortProvider = new FixedPortProvider(new[] { aPortOut });
        nodeB.PortProvider = new FixedPortProvider(new[] { bPortIn });

        // Pair C↔D: pinned ports, unrelated; cache must survive.
        var nodeC = new Node { X = 100, Y = 400 };
        var nodeD = new Node { X = 400, Y = 400 };
        var cPortOut = new Port(nodeC, "out", PortFlow.Output, new PortAnchor(PortEdge.Right, 0.5));
        var dPortIn  = new Port(nodeD, "in",  PortFlow.Input,  new PortAnchor(PortEdge.Left,  0.5));
        nodeC.PortProvider = new FixedPortProvider(new[] { cPortOut });
        nodeD.PortProvider = new FixedPortProvider(new[] { dPortIn });

        graph.AddNode(nodeA); graph.AddNode(nodeB);
        graph.AddNode(nodeC); graph.AddNode(nodeD);

        var ab = new Connection(aPortOut, bPortIn);
        var cd = new Connection(cPortOut, dPortIn);
        graph.AddConnection(ab);
        graph.AddConnection(cd);

        canvas.Graph = graph;
        canvas.Measure(new Size(CanvasWidth, CanvasHeight));
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));

        using (var bmp = new RenderTargetBitmap(new PixelSize(CanvasWidth, CanvasHeight)))
        using (var ctx = bmp.CreateDrawingContext())
            canvas.Render(ctx);

        Assert.True(canvas.ConnectionGeometryCacheContains(ab.Id),
            "AB must be cached after first render.");
        Assert.True(canvas.ConnectionGeometryCacheContains(cd.Id),
            "CD must be cached after first render.");

        // Trigger DistributeAuto on nodeA's edge: add a second auto port to the same edge.
        // This moves aPortOut and invalidates its AbsolutePosition.
        var aPortOut2 = new Port(nodeA, "out2", PortFlow.Output, PortEdge.Right);
        ((FixedPortProvider)nodeA.PortProvider!).AddPort(aPortOut2);

        Assert.False(canvas.ConnectionGeometryCacheContains(ab.Id),
            "AB cache must drop — its source port's AbsolutePosition changed.");
        Assert.True(canvas.ConnectionGeometryCacheContains(cd.Id),
            "CD cache must remain — unrelated to the moved node.");
    }
}
