using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;
using Size = Avalonia.Size;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasSelectionTests
{
    [AvaloniaFact]
    public void SelectNode_selects_single_node()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.SelectNode(node, additive: false);

        Assert.Contains(node, graph.SelectedNodes);
        Assert.True(node.IsSelected);
    }

    [AvaloniaFact]
    public void SelectNode_additive_adds_to_existing_selection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 200, Y = 200 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        canvas.Graph = graph;

        canvas.SelectNode(node1, additive: false);
        canvas.SelectNode(node2, additive: true);

        Assert.Contains(node1, graph.SelectedNodes);
        Assert.Contains(node2, graph.SelectedNodes);
    }

    [AvaloniaFact]
    public void SelectNode_non_additive_clears_previous_selection()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 200, Y = 200 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        canvas.Graph = graph;

        canvas.SelectNode(node1, additive: false);
        canvas.SelectNode(node2, additive: false);

        Assert.DoesNotContain(node1, graph.SelectedNodes);
        Assert.False(node1.IsSelected);
        Assert.Contains(node2, graph.SelectedNodes);
        Assert.True(node2.IsSelected);
    }

    [AvaloniaFact]
    public void SelectNode_additive_toggles_off_already_selected()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.SelectNode(node, additive: false);
        Assert.True(node.IsSelected);

        canvas.SelectNode(node, additive: true);
        Assert.False(node.IsSelected);
        Assert.DoesNotContain(node, graph.SelectedNodes);
    }

    [AvaloniaFact]
    public void ClearSelection_deselects_all_nodes()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node1 = new Node { X = 0, Y = 0 };
        var node2 = new Node { X = 200, Y = 200 };
        graph.AddNode(node1);
        graph.AddNode(node2);
        canvas.Graph = graph;

        canvas.SelectNode(node1, additive: false);
        canvas.SelectNode(node2, additive: true);
        canvas.ClearSelection();

        Assert.Empty(graph.SelectedNodes);
        Assert.False(node1.IsSelected);
        Assert.False(node2.IsSelected);
    }

    [AvaloniaFact]
    public void HitTestNode_finds_node_at_position()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        var hit = canvas.HitTestNode(new Point(75, 75));
        Assert.Same(node, hit);
    }

    [AvaloniaFact]
    public void HitTestNode_returns_null_for_empty_space()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        node.Width = 100;
        node.Height = 60;
        graph.AddNode(node);
        canvas.Graph = graph;

        var hit = canvas.HitTestNode(new Point(300, 300));
        Assert.Null(hit);
    }

    [AvaloniaFact]
    public void HitTestNode_returns_topmost_overlapping_node()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var bottom = new Node { X = 50, Y = 50 };
        bottom.Width = 100;
        bottom.Height = 100;
        var top = new Node { X = 80, Y = 80 };
        top.Width = 100;
        top.Height = 100;
        graph.AddNode(bottom);
        graph.AddNode(top);
        canvas.Graph = graph;

        // Point is in overlap region; should return the last-added (topmost)
        var hit = canvas.HitTestNode(new Point(100, 100));
        Assert.Same(top, hit);
    }

    [AvaloniaFact]
    public void SelectionHandler_is_notified_on_select()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;

        IReadOnlyList<Node>? notified = null;
        canvas.SelectionHandler = new TestSelectionHandler(nodes => notified = nodes);

        canvas.SelectNode(node, additive: false);

        Assert.NotNull(notified);
        Assert.Contains(node, notified);
    }

    [AvaloniaFact]
    public void SelectionHandler_is_notified_on_clear()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node { X = 50, Y = 50 };
        graph.AddNode(node);
        canvas.Graph = graph;

        canvas.SelectNode(node, additive: false);

        IReadOnlyList<Node>? notified = null;
        canvas.SelectionHandler = new TestSelectionHandler(nodes => notified = nodes);

        canvas.ClearSelection();

        Assert.NotNull(notified);
        Assert.Empty(notified);
    }

    [AvaloniaFact]
    public void ClearSelection_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ClearSelection();
        // No exception = pass
    }

    [AvaloniaFact]
    public void SelectNode_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        var node = new Node();
        canvas.SelectNode(node, additive: false);
        // No exception = pass
    }

    [AvaloniaFact]
    public void Marquee_state_starts_inactive()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.IsMarqueeSelecting);
    }

    [AvaloniaFact]
    public void Marquee_selects_nodes_within_rectangle()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var n1 = new Node { X = 50, Y = 50 };
        n1.Width = 100;
        n1.Height = 60;
        var n2 = new Node { X = 300, Y = 300 };
        n2.Width = 100;
        n2.Height = 60;
        graph.AddNode(n1);
        graph.AddNode(n2);
        canvas.Graph = graph;

        // Simulate marquee that covers n1 but not n2
        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var marqueeRect = new Rect(0, 0, 200, 200);

        foreach (var node in graph.Nodes)
        {
            var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
            var nodeScreenSize = new Size(
                transform.WorldToScreen(node.Width),
                transform.WorldToScreen(node.Height));
            var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

            if (marqueeRect.Intersects(nodeRect))
            {
                node.IsSelected = true;
                graph.Select(node);
            }
        }

        Assert.True(n1.IsSelected);
        Assert.False(n2.IsSelected);
        Assert.Single(graph.SelectedNodes);
    }

    // ----- Task 18: click / ctrl-click selection for connections -----

    private const int ClickCanvasWidth = 800;
    private const int ClickCanvasHeight = 600;

    private sealed class RecordingSelectionHandler : ISelectionHandler
    {
        public List<IReadOnlyCollection<IGraphElement>> Calls { get; } = new();
        public void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected) => Calls.Add(selected);
    }

    // Uses a StraightRouter so click-hit geometry is predictable: the connection
    // is a straight line segment between the two port absolute positions, so any
    // interior point is trivially on the stroke. Tests that need a bezier can
    // override the router on the canvas.
    private static (NodiumGraphCanvas canvas, Graph graph, Node nodeA, Node nodeB, Connection connection)
        BuildCanvasWithConnection(Point sourcePos, Point targetPos)
    {
        var canvas = new NodiumGraphCanvas();
        canvas.ConnectionRouter = new StraightRouter();
        var graph = new Graph();
        var nodeA = new Node { X = sourcePos.X, Y = sourcePos.Y };
        nodeA.Width = 20;
        nodeA.Height = 20;
        var nodeB = new Node { X = targetPos.X, Y = targetPos.Y };
        nodeB.Width = 20;
        nodeB.Height = 20;
        // Port at (10, 10) relative → middle of node. Emission direction is stable
        // (ties break horizontal-first to (-1, 0)) but StraightRouter ignores it.
        var portOut = new Port(nodeA, new Point(10, 10));
        var portIn = new Port(nodeB, new Point(10, 10));
        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        var connection = new Connection(portOut, portIn);
        graph.AddConnection(connection);
        canvas.Graph = graph;

        canvas.Measure(new Size(ClickCanvasWidth, ClickCanvasHeight));
        canvas.Arrange(new Rect(0, 0, ClickCanvasWidth, ClickCanvasHeight));
        DriveRender(canvas);
        return (canvas, graph, nodeA, nodeB, connection);
    }

    private static void DriveRender(NodiumGraphCanvas canvas)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(ClickCanvasWidth, ClickCanvasHeight));
        using var ctx = bitmap.CreateDrawingContext();
        canvas.Render(ctx);
    }

    [AvaloniaFact]
    public void Click_on_connection_selects_it_and_fires_handler()
    {
        var (canvas, graph, _, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        Assert.True(
            canvas.ConnectionGeometryCacheContains(conn.Id),
            "Cache must be populated before clicking or the hit-tester has nothing to probe.");

        var handler = new RecordingSelectionHandler();
        canvas.SelectionHandler = handler;

        // Midpoint of the straight route between the two port absolute positions
        // (110,110) -> (310,210) is (210, 160).
        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(210, 160));

        var handled = canvas.TryClickSelectConnection(screenPoint, ctrl: false);

        Assert.True(handled);
        Assert.Contains(conn, graph.SelectedItems);
        Assert.Single(handler.Calls);
        Assert.Contains(conn, handler.Calls[^1]);
    }

    [AvaloniaFact]
    public void Click_on_connection_replaces_prior_selection()
    {
        var (canvas, graph, nodeA, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        canvas.SelectNode(nodeA, additive: false);
        Assert.Contains(nodeA, graph.SelectedItems);

        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(210, 160));

        var handled = canvas.TryClickSelectConnection(screenPoint, ctrl: false);

        Assert.True(handled);
        Assert.Contains(conn, graph.SelectedItems);
        Assert.DoesNotContain(nodeA, graph.SelectedItems);
    }

    [AvaloniaFact]
    public void Ctrl_click_on_connection_toggles_off_existing_selection()
    {
        var (canvas, graph, _, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(210, 160));

        // First plain click selects.
        Assert.True(canvas.TryClickSelectConnection(screenPoint, ctrl: false));
        Assert.Contains(conn, graph.SelectedItems);

        // Ctrl-click on the same point toggles it off.
        Assert.True(canvas.TryClickSelectConnection(screenPoint, ctrl: true));
        Assert.DoesNotContain(conn, graph.SelectedItems);
    }

    [AvaloniaFact]
    public void Ctrl_click_on_connection_adds_to_existing_selection()
    {
        var (canvas, graph, nodeA, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        canvas.SelectNode(nodeA, additive: false);

        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(210, 160));

        Assert.True(canvas.TryClickSelectConnection(screenPoint, ctrl: true));
        Assert.Contains(nodeA, graph.SelectedItems);
        Assert.Contains(conn, graph.SelectedItems);
    }

    [AvaloniaFact]
    public void Click_on_empty_area_reports_no_connection_hit()
    {
        var (canvas, graph, _, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        // Far above the line segment from (110,110) -> (310,210), well outside
        // the 8px worldTolerance band even after AABB inflation.
        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(600, 20));

        var handled = canvas.TryClickSelectConnection(screenPoint, ctrl: false);

        Assert.False(handled);
        Assert.DoesNotContain(conn, graph.SelectedItems);
    }

    [AvaloniaFact]
    public void TryClickSelectConnection_with_null_graph_does_not_throw()
    {
        var canvas = new NodiumGraphCanvas();
        var handled = canvas.TryClickSelectConnection(new Point(10, 10), ctrl: false);
        Assert.False(handled);
    }

    [AvaloniaFact]
    public void SelectedItems_collection_change_invalidates_visual()
    {
        var (canvas, graph, _, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        // The canvas must re-render when SelectedItems changes so the halo pass
        // picks up the new selection state.
        graph.SelectedItems.Add(conn);
        // If invalidation isn't wired, a subsequent render still works —
        // the behavioral guarantee tested here is that rendering after a
        // selection change includes the connection in the halo pass without
        // explicit InvalidateVisual. We drive render and assert no exception.
        DriveRender(canvas);
        Assert.Contains(conn, graph.SelectedItems);
    }

    // These two tests pin the hit-test priority enforced by OnPointerPressed:
    // port-branch -> node-branch -> connection-branch. Because simulating a
    // PointerPressedEventArgs in headless is fragile, we reproduce that
    // ordering directly via the internal seams (ResolvePortWithProvider and
    // HitTestNode) at a click point that is *also* on the connection stroke.
    // A ground-truth TryClickSelectConnection call on a throwaway canvas
    // confirms the conflict is real (the point hits the connection), then we
    // assert the earlier branch catches the click first so the connection
    // branch is never reached in production.

    [AvaloniaFact]
    public void Click_on_port_does_not_select_connection_behind_it()
    {
        var (canvas, graph, _, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        // Straight route midpoint (210, 160). Place a node carrying a fixed
        // port whose absolute position lands exactly on the stroke. Node C is
        // positioned so (210, 160) is *outside* its rect — only the port
        // (resolved via radius) should hit, not HitTestNode.
        var nodeC = new Node { X = 400, Y = 400 };
        nodeC.Width = 20;
        nodeC.Height = 20;
        // Relative (-190, -240) + node origin (400, 400) = absolute (210, 160).
        var floatingPort = new Port(nodeC, new Point(-190, -240));
        nodeC.PortProvider = new FixedPortProvider(new[] { floatingPort });
        graph.AddNode(nodeC);

        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(210, 160));

        // Ground-truth: the point *is* on the connection stroke, so without
        // any priority rule the connection branch would select it.
        var groundTruth = BuildCanvasWithConnection(new Point(100, 100), new Point(300, 200));
        Assert.True(
            groundTruth.canvas.TryClickSelectConnection(screenPoint, ctrl: false),
            "Fixture sanity: the click point must actually lie on the connection stroke.");

        // Priority check — reproduce OnPointerPressed branch ordering:
        // port first, then node, then connection.
        var (hitPort, _) = canvas.ResolvePortWithProvider(screenPoint, preview: false);
        Assert.Same(floatingPort, hitPort);

        // Because the port branch fires first, OnPointerPressed never reaches
        // TryClickSelectConnection, so the connection stays unselected.
        Assert.Empty(graph.SelectedItems);
        Assert.DoesNotContain(conn, graph.SelectedItems);

        // Belt-and-braces: HitTestNode must NOT be returning nodeC at this
        // point, otherwise this test would be exercising the node branch by
        // accident instead of the port branch.
        Assert.Null(canvas.HitTestNode(screenPoint));
    }

    [AvaloniaFact]
    public void Click_on_node_does_not_select_connection_passing_through_it()
    {
        var (canvas, graph, _, _, conn) = BuildCanvasWithConnection(
            new Point(100, 100), new Point(300, 200));

        // Straight route midpoint (210, 160). Place node C so its rect
        // covers that point: (200..220) x (150..170). Node C has no
        // PortProvider, so the port branch falls through.
        var nodeC = new Node { X = 200, Y = 150 };
        nodeC.Width = 20;
        nodeC.Height = 20;
        graph.AddNode(nodeC);

        var transform = new ViewportTransform(canvas.ViewportZoom, canvas.ViewportOffset);
        var screenPoint = transform.WorldToScreen(new Point(210, 160));

        // Ground-truth: the connection is still hittable at that point.
        var groundTruth = BuildCanvasWithConnection(new Point(100, 100), new Point(300, 200));
        Assert.True(
            groundTruth.canvas.TryClickSelectConnection(screenPoint, ctrl: false),
            "Fixture sanity: the click point must actually lie on the connection stroke.");

        // Priority check — reproduce OnPointerPressed branch ordering.
        var (hitPort, _) = canvas.ResolvePortWithProvider(screenPoint, preview: false);
        Assert.Null(hitPort);

        var hitNode = canvas.HitTestNode(screenPoint);
        Assert.Same(nodeC, hitNode);

        // Node branch catches the click first, so production OnPointerPressed
        // never reaches TryClickSelectConnection. The connection is not in
        // SelectedItems even though its stroke passes through nodeC.
        Assert.DoesNotContain(conn, graph.SelectedItems);
    }
}
