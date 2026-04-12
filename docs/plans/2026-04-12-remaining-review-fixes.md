# Remaining Code Review Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Fix all remaining issues from the 2026-04-11 full code review — 2 critical correctness bugs, 6 performance issues, 1 lifecycle fix, and 2 API design improvements.

**Architecture:** Fixes are grouped into 10 independent tasks by subsystem. Correctness fixes first (selection desync, source port commit, hit-test ordering), then performance (render allocations, scalability), then lifecycle and API improvements. Each task is self-contained and produces a working build with passing tests.

**Tech Stack:** C# / .NET 10 / Avalonia 12 / xUnit v3 headless

**Test patterns:** Tests use `[AvaloniaFact]` or `[Fact]` attributes. The test project has `InternalsVisibleTo` access. Canvas tests instantiate `NodiumGraphCanvas` directly. Ports are created via `new Port(node, position)`.

---

## File Map

| File | Changes |
|------|---------|
| `src/NodiumGraph/Model/Graph.cs` | Task 1: Sync `IsSelected` in `Select`/`Deselect`/`ClearSelection`. Task 4: `HashSet` backing + `RemoveNodes` batch |
| `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` | Task 2: Track `_sourceProvider` for connection source commit/cancel. Task 3: Iterate `Graph.Nodes` in `HitTestNode`. Task 8: Implement `IDisposable` |
| `src/NodiumGraph/Controls/CanvasOverlay.cs` | Task 5: Cache pens/brushes as fields, invalidate on resource change |
| `src/NodiumGraph/Controls/GridRenderer.cs` | Task 5: Hoist pen creation out of loop body |
| `src/NodiumGraph/Controls/ConnectionRenderer.cs` | Task 6: Replace `PathGeometry` with `StreamGeometry` |
| `src/NodiumGraph/Controls/MinimapRenderer.cs` | Task 7: Single-pass bounds computation, shared helper |
| `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` | Task 9: Reuse buffer in `CuttingLineIntersectsGeometry` |
| `src/NodiumGraph/Interactions/IConnectionRouter.cs` | Task 10: Replace `IsBezierRoute` with `RouteKind` enum |
| `src/NodiumGraph/Interactions/BezierRouter.cs` | Task 10: Return `RouteKind.Bezier` |
| `src/NodiumGraph/Interactions/StepRouter.cs` | Task 10: Return `RouteKind.Polyline` |
| `src/NodiumGraph/Interactions/StraightRouter.cs` | Task 10: Return `RouteKind.Polyline` |
| `tests/NodiumGraph.Tests/GraphTests.cs` | Tasks 1, 4 |
| `tests/NodiumGraph.Tests/NodiumGraphCanvasMethodTests.cs` | Task 3 |
| `tests/NodiumGraph.Tests/NodiumGraphCanvasConnectionDrawTests.cs` | Task 2 |
| `tests/NodiumGraph.Tests/ConnectionRendererTests.cs` | Task 6 |
| `tests/NodiumGraph.Tests/MinimapTests.cs` | Task 7 |
| `tests/NodiumGraph.Tests/NodiumGraphCanvasCuttingTests.cs` | Task 9 |

---

### Task 1: Fix Graph.Select/Deselect/ClearSelection to sync Node.IsSelected [Critical]

**Files:**
- Modify: `src/NodiumGraph/Model/Graph.cs` — `Select`, `Deselect`, `ClearSelection` methods
- Test: `tests/NodiumGraph.Tests/GraphTests.cs`

**Context:** `Graph.Select(node)` adds to `_selectedNodes` but does NOT set `node.IsSelected = true`. The canvas does both via `SelectNode()`, but if a consumer calls `Graph.Select()` directly (it's a public API), selection state desynchronizes — the node is in `SelectedNodes` but `IsSelected` is `false`, so no visual border.

- [ ] **Step 1: Write the failing tests**

In `GraphTests.cs`, add:

```csharp
[Fact]
public void Select_sets_IsSelected_true()
{
    var graph = new Graph();
    var node = new Node();
    graph.AddNode(node);

    graph.Select(node);

    Assert.True(node.IsSelected);
}

[Fact]
public void Deselect_sets_IsSelected_false()
{
    var graph = new Graph();
    var node = new Node();
    graph.AddNode(node);
    graph.Select(node);

    graph.Deselect(node);

    Assert.False(node.IsSelected);
}

[Fact]
public void ClearSelection_resets_all_IsSelected()
{
    var graph = new Graph();
    var a = new Node();
    var b = new Node();
    graph.AddNode(a);
    graph.AddNode(b);
    graph.Select(a);
    graph.Select(b);

    graph.ClearSelection();

    Assert.False(a.IsSelected);
    Assert.False(b.IsSelected);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "Select_sets_IsSelected_true|Deselect_sets_IsSelected_false|ClearSelection_resets_all_IsSelected"`
Expected: 3 FAILs

- [ ] **Step 3: Implement the fix**

In `Graph.cs`:

**`Select`** — add after `_selectedNodes.Add(node)`:
```csharp
node.IsSelected = true;
```

**`Deselect`** — add before `_selectedNodes.Remove(node)`:
```csharp
node.IsSelected = false;
```

**`ClearSelection`** — replace body:
```csharp
public void ClearSelection()
{
    foreach (var node in _selectedNodes)
        node.IsSelected = false;
    _selectedNodes.Clear();
}
```

Also update `NodiumGraphCanvas.SelectNode` to remove the now-redundant `n.IsSelected = false` and `node.IsSelected = true/false` lines — `Graph.Select/Deselect` handles them now. The canvas method should just call `Graph.Select/Deselect` and let the model manage `IsSelected`.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Model/Graph.cs src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/GraphTests.cs
git commit -m "fix: sync Node.IsSelected in Graph.Select/Deselect/ClearSelection"
```

---

### Task 2: Track and commit/cancel source port provider during connection draw [Critical]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — add `_sourceProvider` field, update `OnPointerPressed` and `OnPointerReleased`
- Test: `tests/NodiumGraph.Tests/NodiumGraphCanvasConnectionDrawTests.cs`

**Context:** In `OnPointerPressed`, the source port is resolved with `preview: true` via `ResolvePort()`. With `DynamicPortProvider`, `preview: true` never creates ports — so connections can only start from existing ports on dynamic nodes. Additionally, the source provider never gets a commit/cancel call, leaking state.

The fix:
1. Resolve source port with `preview: false` so `DynamicPortProvider` creates the port
2. Track `_sourceProvider` — the `IPortProvider` that resolved the source port
3. On connection cancel/failure: call `_sourceProvider.CancelResolve()` to remove the tentative port
4. On connection success: port stays (no action needed on source provider)

- [ ] **Step 1: Write the failing test**

In `NodiumGraphCanvasConnectionDrawTests.cs`, add a test that verifies the source provider's port is cleaned up on cancel:

```csharp
[AvaloniaFact]
public void Connection_cancel_removes_dynamic_source_port()
{
    var canvas = new NodiumGraphCanvas();
    var graph = new Graph();
    var sourceNode = new Node { X = 0, Y = 0 };
    sourceNode.Width = 100;
    sourceNode.Height = 100;
    var dynamicProvider = new DynamicPortProvider(sourceNode);
    sourceNode.PortProvider = dynamicProvider;

    var targetNode = new Node { X = 300, Y = 0 };
    targetNode.Width = 100;
    targetNode.Height = 100;
    graph.AddNode(sourceNode);
    graph.AddNode(targetNode);
    canvas.Graph = graph;

    // Before connection attempt
    Assert.Empty(dynamicProvider.Ports);

    // Simulate: resolve source port with preview: false (the fix)
    var sourcePort = sourceNode.PortProvider.ResolvePort(
        new Point(100, 50), preview: false);
    Assert.NotNull(sourcePort);
    Assert.Single(dynamicProvider.Ports);

    // Cancel should remove the port
    dynamicProvider.CancelResolve();
    Assert.Empty(dynamicProvider.Ports);
}
```

- [ ] **Step 2: Run test to verify it passes (baseline)**

Run: `dotnet test --filter "Connection_cancel_removes_dynamic_source_port"`
Expected: PASS (this validates the DynamicPortProvider mechanics work correctly)

- [ ] **Step 3: Implement the fix**

In `NodiumGraphCanvas.cs`:

Add field alongside existing `_commitProvider`:
```csharp
private IPortProvider? _sourceProvider;
```

In `ResolvePort` method (line 348), change it to also track which provider matched. Add a new overload or modify to return the provider:

```csharp
internal (Port? port, IPortProvider? provider) ResolvePortWithProvider(
    Point screenPosition, bool preview)
{
    if (Graph == null) return (null, null);
    var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
    var worldPosition = transform.ScreenToWorld(screenPosition);

    foreach (var node in Graph.Nodes)
    {
        if (node.IsCollapsed) continue;
        if (node.PortProvider == null) continue;
        var port = node.PortProvider.ResolvePort(worldPosition, preview);
        if (port != null) return (port, node.PortProvider);
    }
    return (null, null);
}
```

In `OnPointerPressed`, change the source port resolution (around line 583):
```csharp
// Before:
var hitPort = ResolvePort(position, preview: true);
if (hitPort != null)
{
    _isDrawingConnection = true;
    _connectionSourcePort = hitPort;

// After:
var (hitPort, sourceProvider) = ResolvePortWithProvider(position, preview: false);
if (hitPort != null)
{
    _isDrawingConnection = true;
    _connectionSourcePort = hitPort;
    _sourceProvider = sourceProvider;
```

In `OnPointerReleased`, inside the `finally` block (around line 845), add source provider cleanup:
```csharp
finally
{
    _commitProvider = null;
    _sourceProvider = null;
    _isDrawingConnection = false;
    _connectionSourcePort = null;
    _connectionTargetPort = null;
}
```

And in the `if (!connected)` block, cancel the source provider too:
```csharp
if (!connected)
{
    _commitProvider?.CancelResolve();
    _sourceProvider?.CancelResolve();
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/NodiumGraphCanvasConnectionDrawTests.cs
git commit -m "fix: track source port provider for commit/cancel during connection draw"
```

---

### Task 3: Fix HitTestNode z-order — iterate Graph.Nodes instead of Dictionary [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — `HitTestNode` method (line 364)
- Test: `tests/NodiumGraph.Tests/NodiumGraphCanvasMethodTests.cs`

**Context:** `HitTestNode` iterates `_nodeContainers` (`Dictionary<Node, ContentControl>`) which has no guaranteed iteration order. The comment says "keep last match (topmost)" but dictionary order doesn't correspond to visual z-order. `Graph.Nodes` is an `ObservableCollection` with stable insertion order — iterate that instead and use `_nodeContainers` only for bounds lookup.

- [ ] **Step 1: Write the failing test**

In `NodiumGraphCanvasMethodTests.cs`, add:

```csharp
[AvaloniaFact]
public void HitTestNode_returns_last_added_when_overlapping()
{
    var canvas = new NodiumGraphCanvas();
    var graph = new Graph();

    // Two nodes at the same position — later-added should win
    var first = new Node { X = 0, Y = 0 };
    first.Width = 100;
    first.Height = 100;
    var second = new Node { X = 0, Y = 0 };
    second.Width = 100;
    second.Height = 100;

    graph.AddNode(first);
    graph.AddNode(second);
    canvas.Graph = graph;

    // Hit-test at center — should return "second" (topmost = last in collection)
    var hit = canvas.HitTestNode(new Point(50, 50));
    Assert.Same(second, hit);
}
```

- [ ] **Step 2: Run test — may pass or fail depending on dictionary iteration order**

Run: `dotnet test --filter "HitTestNode_returns_last_added_when_overlapping"`
Note: May pass coincidentally. The point is to guarantee deterministic behavior.

- [ ] **Step 3: Implement the fix**

Replace `HitTestNode` body (line 364-382):

```csharp
internal Node? HitTestNode(Point screenPosition)
{
    if (Graph == null) return null;
    var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
    Node? result = null;

    // Iterate Graph.Nodes (stable insertion order) instead of _nodeContainers (Dictionary).
    // Last match wins = topmost in z-order.
    foreach (var node in Graph.Nodes)
    {
        var nodeScreenPos = transform.WorldToScreen(new Point(node.X, node.Y));
        var nodeScreenSize = new Size(
            transform.WorldToScreen(node.Width),
            transform.WorldToScreen(node.Height));
        var nodeRect = new Rect(nodeScreenPos, nodeScreenSize);

        if (nodeRect.Contains(screenPosition))
            result = node;
    }

    return result;
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/NodiumGraphCanvasMethodTests.cs
git commit -m "fix: HitTestNode iterates Graph.Nodes for deterministic z-order"
```

---

### Task 4: Graph scalability — HashSet backing for Select + batch RemoveNodes [Important]

**Files:**
- Modify: `src/NodiumGraph/Model/Graph.cs` — add `_selectedSet`, add `RemoveNodes` overload
- Test: `tests/NodiumGraph.Tests/GraphTests.cs`

**Context:** `Graph.Select` uses `_selectedNodes.Contains(node)` which is O(n) on `List<Node>`. With 500+ nodes, batch select-all is O(n²). Also, `RemoveNode` is O(n) per connection with individual `CollectionChanged` events — there's a TODO at line 44 requesting a batch overload.

- [ ] **Step 1: Write the failing tests**

In `GraphTests.cs`, add:

```csharp
[Fact]
public void Select_same_node_twice_is_idempotent()
{
    var graph = new Graph();
    var node = new Node();
    graph.AddNode(node);

    graph.Select(node);
    graph.Select(node); // should not throw or duplicate

    Assert.Single(graph.SelectedNodes);
}

[Fact]
public void RemoveNodes_batch_removes_all_with_connections()
{
    var graph = new Graph();
    var a = new Node { X = 0, Y = 0 };
    var b = new Node { X = 100, Y = 0 };
    var c = new Node { X = 200, Y = 0 };
    a.PortProvider = new FixedPortProvider(new Port(a, new Point(50, 50)));
    b.PortProvider = new FixedPortProvider(new Port(b, new Point(0, 50)));
    c.PortProvider = new FixedPortProvider(new Port(c, new Point(0, 50)));

    graph.AddNode(a);
    graph.AddNode(b);
    graph.AddNode(c);

    var conn1 = new Connection(a.PortProvider.Ports[0], b.PortProvider.Ports[0]);
    var conn2 = new Connection(a.PortProvider.Ports[0], c.PortProvider.Ports[0]);
    graph.AddConnection(conn1);
    graph.AddConnection(conn2);

    graph.RemoveNodes(new[] { a, b });

    Assert.Single(graph.Nodes);
    Assert.Same(c, graph.Nodes[0]);
    Assert.Empty(graph.Connections);
}

[Fact]
public void RemoveNodes_batch_clears_IsSelected()
{
    var graph = new Graph();
    var a = new Node();
    var b = new Node();
    graph.AddNode(a);
    graph.AddNode(b);
    graph.Select(a);
    graph.Select(b);

    graph.RemoveNodes(new[] { a, b });

    Assert.False(a.IsSelected);
    Assert.False(b.IsSelected);
    Assert.Empty(graph.SelectedNodes);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "RemoveNodes_batch"`
Expected: FAIL — `RemoveNodes` method does not exist

- [ ] **Step 3: Implement the fixes**

In `Graph.cs`:

Add `HashSet` field alongside existing `_selectedNodes`:
```csharp
private readonly HashSet<Node> _selectedSet = new();
```

Update `Select`:
```csharp
public void Select(Node node)
{
    ArgumentNullException.ThrowIfNull(node);
    if (!_nodes.Contains(node))
        throw new InvalidOperationException("Node is not part of this graph.");
    if (_selectedSet.Add(node))
    {
        _selectedNodes.Add(node);
        node.IsSelected = true;
    }
}
```

Update `Deselect`:
```csharp
public void Deselect(Node node)
{
    if (_selectedSet.Remove(node))
    {
        node.IsSelected = false;
        _selectedNodes.Remove(node);
    }
}
```

Update `ClearSelection`:
```csharp
public void ClearSelection()
{
    foreach (var node in _selectedNodes)
        node.IsSelected = false;
    _selectedNodes.Clear();
    _selectedSet.Clear();
}
```

Update `RemoveNode` to also clear `_selectedSet`:
```csharp
node.IsSelected = false;
_selectedSet.Remove(node);
_selectedNodes.Remove(node);
_nodes.Remove(node);
```

Add batch overload:
```csharp
/// <summary>
/// Removes multiple nodes and all connections referencing their ports in a single pass.
/// </summary>
public void RemoveNodes(IEnumerable<Node> nodes)
{
    ArgumentNullException.ThrowIfNull(nodes);
    var nodeSet = new HashSet<Node>(nodes);
    if (nodeSet.Count == 0) return;

    // Collect all affected connections in one pass
    var connectionsToRemove = _connections
        .Where(c => nodeSet.Contains(c.SourcePort.Owner) || nodeSet.Contains(c.TargetPort.Owner))
        .ToList();

    foreach (var conn in connectionsToRemove)
        _connections.Remove(conn);

    foreach (var node in nodeSet)
    {
        node.IsSelected = false;
        _selectedSet.Remove(node);
        _selectedNodes.Remove(node);
        _nodes.Remove(node);
    }
}
```

Remove the TODO comment on line 44.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Model/Graph.cs tests/NodiumGraph.Tests/GraphTests.cs
git commit -m "perf: HashSet-backed selection O(1) contains, add RemoveNodes batch overload"
```

---

### Task 5: Cache pens and brushes in CanvasOverlay and GridRenderer [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs` — cache default pens as fields
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs` — accept pre-built pens or hoist allocation

**Context:** Every `Render()` call in `CanvasOverlay` creates `selectedBorderPen`, `hoveredBorderPen`, `defaultPortPen`, preview pens, and cutting pens. `GridRenderer.RenderLines` creates `minorPen` and `majorPen` every frame. These are pure GC pressure with no benefit.

- [ ] **Step 1: Cache pens in CanvasOverlay**

Add private fields to `CanvasOverlay`:
```csharp
private Pen? _cachedSelectedBorderPen;
private Pen? _cachedHoveredBorderPen;
private Pen? _cachedPortOutlinePen;
private IBrush? _lastSelectedBrush;
private double _lastSelectedThickness;
private IBrush? _lastHoveredBrush;
private double _lastHoveredThickness;
private IBrush? _lastPortOutlineBrush;
```

Add a helper to create-or-reuse:
```csharp
private static Pen GetOrCreatePen(ref Pen? cached, ref IBrush? lastBrush,
    ref double lastThickness, IBrush brush, double thickness)
{
    if (cached != null && ReferenceEquals(lastBrush, brush)
        && Math.Abs(lastThickness - thickness) < 0.001)
        return cached;

    lastBrush = brush;
    lastThickness = thickness;
    cached = new Pen(brush, thickness);
    return cached;
}
```

Replace the pen allocations in `Render` with calls to `GetOrCreatePen`.

- [ ] **Step 2: Hoist pens in GridRenderer.RenderLines**

`RenderLines` is a `static` method, so we can't use instance fields. Instead, make the caller responsible. In `GridRenderer.Render` (the public entry point), create pens once and pass them through:

The current `RenderLines` already creates pens at the top of the method (lines 79-80) — they're already hoisted out of the loop. The allocation is once per frame, not per line. This is acceptable. No change needed for GridRenderer unless you want to move pen creation into the caller.

**Decision:** Skip GridRenderer — the pens are already created once per `RenderLines` call, not per line iteration. The review finding was imprecise about the actual hot path.

- [ ] **Step 3: Cache FormattedText for port labels**

Port labels with the same text + size + brush can be cached. Add a dictionary cache to `CanvasOverlay`:

```csharp
private readonly Dictionary<(string label, double fontSize), FormattedText> _labelCache = new();
```

In the port label rendering loop, replace:
```csharp
var text = new FormattedText(
    port.Label, CultureInfo.InvariantCulture, ...);
```
With:
```csharp
var key = (port.Label, portLabelFontSize * zoom);
if (!_labelCache.TryGetValue(key, out var text))
{
    text = new FormattedText(
        port.Label, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Typeface.Default,
        portLabelFontSize * zoom, portLabelBrush);
    _labelCache[key] = text;
}
```

Add cache invalidation in a method called when zoom changes or resources change:
```csharp
internal void InvalidateLabelCache() => _labelCache.Clear();
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/CanvasOverlay.cs
git commit -m "perf: cache pens and FormattedText in CanvasOverlay to reduce render allocations"
```

---

### Task 6: ConnectionRenderer — use StreamGeometry instead of PathGeometry [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/ConnectionRenderer.cs` — replace `PathGeometry` with `StreamGeometry`
- Test: `tests/NodiumGraph.Tests/ConnectionRendererTests.cs`

**Context:** `CreateGeometry` creates `PathGeometry` + `PathFigure` + segments per connection per frame. `StreamGeometry` is Avalonia's lightweight alternative — it uses a compact binary representation, doesn't support modification after creation, and is cheaper to render.

- [ ] **Step 1: Rewrite CreateGeometry with StreamGeometry**

Replace the method body:

```csharp
public static Geometry CreateGeometry(
    Connection connection, IConnectionRouter router, ViewportTransform transform)
{
    var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
    if (routePoints.Count < 2)
        return new StreamGeometry();

    var geo = new StreamGeometry();
    using (var ctx = geo.Open())
    {
        ctx.BeginFigure(transform.WorldToScreen(routePoints[0]), false);

        if (router.IsBezierRoute && routePoints.Count == 4)
        {
            ctx.CubicBezierTo(
                transform.WorldToScreen(routePoints[1]),
                transform.WorldToScreen(routePoints[2]),
                transform.WorldToScreen(routePoints[3]));
        }
        else
        {
            for (var i = 1; i < routePoints.Count; i++)
                ctx.LineTo(transform.WorldToScreen(routePoints[i]));
        }

        ctx.EndFigure(false);
    }

    return geo;
}
```

This eliminates `List<Point>`, `PathFigure`, `BezierSegment`, and `LineSegment` allocations. `StreamGeometry` is the idiomatic Avalonia choice for immutable paths.

- [ ] **Step 2: Run existing ConnectionRenderer tests**

Run: `dotnet test --filter "ConnectionRenderer"`
Expected: All PASS — `StreamGeometry` is a subclass of `Geometry`, so the `Render` method works unchanged

- [ ] **Step 3: Also update CuttingLineIntersectsGeometry to skip intermediate List**

This is actually Task 9 — skip for now.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/ConnectionRenderer.cs
git commit -m "perf: use StreamGeometry in ConnectionRenderer to reduce per-frame allocations"
```

---

### Task 7: MinimapRenderer — single-pass bounds computation [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/MinimapRenderer.cs` — extract `ComputeWorldBounds`, use in `Render` and `MinimapToWorld`
- Test: `tests/NodiumGraph.Tests/MinimapTests.cs`

**Context:** `Render` calls `.Min()` / `.Max()` 4 times (4 full iterations of all nodes). `MinimapToWorld` repeats the same computation. With 500 nodes, that's 2000+ iterations per frame for the minimap alone.

- [ ] **Step 1: Write a test for ComputeWorldBounds**

In `MinimapTests.cs`, add:

```csharp
[Fact]
public void ComputeWorldBounds_returns_correct_bounds()
{
    var graph = new Graph();
    var a = new Node { X = 10, Y = 20 };
    a.Width = 50;
    a.Height = 30;
    var b = new Node { X = 100, Y = 80 };
    b.Width = 60;
    b.Height = 40;
    graph.AddNode(a);
    graph.AddNode(b);

    var bounds = MinimapRenderer.ComputeWorldBounds(graph);

    Assert.NotNull(bounds);
    Assert.Equal(10, bounds.Value.minX);
    Assert.Equal(20, bounds.Value.minY);
    Assert.Equal(160, bounds.Value.maxX);  // 100 + 60
    Assert.Equal(120, bounds.Value.maxY);  // 80 + 40
}

[Fact]
public void ComputeWorldBounds_returns_null_for_empty_graph()
{
    var graph = new Graph();
    Assert.Null(MinimapRenderer.ComputeWorldBounds(graph));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "ComputeWorldBounds"`
Expected: FAIL — method does not exist

- [ ] **Step 3: Implement ComputeWorldBounds and refactor Render/MinimapToWorld**

Add to `MinimapRenderer`:

```csharp
public static (double minX, double minY, double maxX, double maxY)? ComputeWorldBounds(Graph graph)
{
    if (graph.Nodes.Count == 0) return null;

    var minX = double.MaxValue;
    var minY = double.MaxValue;
    var maxX = double.MinValue;
    var maxY = double.MinValue;

    foreach (var node in graph.Nodes)
    {
        if (node.X < minX) minX = node.X;
        if (node.Y < minY) minY = node.Y;
        var right = node.X + node.Width;
        var bottom = node.Y + node.Height;
        if (right > maxX) maxX = right;
        if (bottom > maxY) maxY = bottom;
    }

    return (minX, minY, maxX, maxY);
}
```

Refactor `Render` to call `ComputeWorldBounds` once:
```csharp
var rawBounds = ComputeWorldBounds(graph);
if (rawBounds is null) return;
var (minX, minY, maxX, maxY) = rawBounds.Value;
// ... rest of method uses minX/minY/maxX/maxY
```

Refactor `MinimapToWorld` similarly — replace the 4 LINQ calls with `ComputeWorldBounds`.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/MinimapRenderer.cs tests/NodiumGraph.Tests/MinimapTests.cs
git commit -m "perf: single-pass world bounds in MinimapRenderer, eliminate 4x LINQ iterations"
```

---

### Task 8: Implement IDisposable on NodiumGraphCanvas [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — implement `IDisposable`
- Test: `tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs`

**Context:** The canvas subscribes to external events (Graph, Node, Port PropertyChanged; IPortProvider events). `OnDetachedFromVisualTree` handles cleanup when the canvas is removed from the visual tree. But if the canvas is created, assigned a graph, and never attached (test scenarios, dynamic UI), subscriptions leak. `IDisposable` provides a fallback cleanup path.

- [ ] **Step 1: Write the failing test**

In `NodiumGraphCanvasGraphBindingTests.cs`, add:

```csharp
[AvaloniaFact]
public void Dispose_clears_subscriptions_without_visual_tree()
{
    var canvas = new NodiumGraphCanvas();
    var graph = new Graph();
    var node = new Node();
    graph.AddNode(node);
    canvas.Graph = graph;

    Assert.Equal(1, canvas.NodeContainerCount);

    canvas.Dispose();

    // After dispose, graph changes should not affect canvas
    graph.AddNode(new Node());
    Assert.Equal(0, canvas.NodeContainerCount);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "Dispose_clears_subscriptions_without_visual_tree"`
Expected: FAIL — `NodiumGraphCanvas` does not implement `IDisposable`

- [ ] **Step 3: Implement IDisposable**

In `NodiumGraphCanvas.cs`:

Change class declaration:
```csharp
public class NodiumGraphCanvas : TemplatedControl, IDisposable
```

Add field:
```csharp
private bool _disposed;
```

Add `Dispose` method:
```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    if (Graph != null)
        OnGraphChanged(Graph, null);

    GC.SuppressFinalize(this);
}
```

In `OnDetachedFromVisualTree`, call `Dispose()` instead of duplicating teardown:
```csharp
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnDetachedFromVisualTree(e);
    if (Graph != null)
        OnGraphChanged(Graph, null);
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs
git commit -m "fix: implement IDisposable on NodiumGraphCanvas for subscription cleanup"
```

---

### Task 9: Eliminate allocation in CuttingLineIntersectsGeometry [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — `CuttingLineIntersectsGeometry` method (line 1054)

**Context:** `routePoints.Select(transform.WorldToScreen).ToList()` allocates a new `List<Point>` per connection during cutting. With 1000 connections, that's 1000 list allocations per pointer-move event. Replace with direct iteration.

- [ ] **Step 1: Rewrite to iterate without materializing**

Replace the method body:

```csharp
internal bool CuttingLineIntersectsGeometry(
    Point lineStart, Point lineEnd, Connection connection, ViewportTransform transform)
{
    var routePoints = ConnectionRouter.Route(connection.SourcePort, connection.TargetPort);

    if (ConnectionRouter.IsBezierRoute && routePoints.Count == 4)
    {
        var p0 = transform.WorldToScreen(routePoints[0]);
        var p1 = transform.WorldToScreen(routePoints[1]);
        var p2 = transform.WorldToScreen(routePoints[2]);
        var p3 = transform.WorldToScreen(routePoints[3]);

        var prev = p0;
        for (var t = 0.05; t <= 1.0; t += 0.05)
        {
            var current = BezierPoint(p0, p1, p2, p3, t);
            if (LinesIntersect(lineStart, lineEnd, prev, current))
                return true;
            prev = current;
        }
        return false;
    }

    // Polyline — iterate route points directly
    var prevScreen = transform.WorldToScreen(routePoints[0]);
    for (var i = 1; i < routePoints.Count; i++)
    {
        var currentScreen = transform.WorldToScreen(routePoints[i]);
        if (LinesIntersect(lineStart, lineEnd, prevScreen, currentScreen))
            return true;
        prevScreen = currentScreen;
    }
    return false;
}
```

- [ ] **Step 2: Run cutting tests**

Run: `dotnet test --filter "Cutting"`
Expected: All PASS

- [ ] **Step 3: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 4: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "perf: eliminate List allocation in CuttingLineIntersectsGeometry"
```

---

### Task 10: Replace IConnectionRouter.IsBezierRoute with RouteKind enum [Suggestion]

**Files:**
- Create: `src/NodiumGraph/Interactions/RouteKind.cs`
- Modify: `src/NodiumGraph/Interactions/IConnectionRouter.cs` — replace `IsBezierRoute` with `RouteKind`
- Modify: `src/NodiumGraph/Interactions/BezierRouter.cs`
- Modify: `src/NodiumGraph/Interactions/StepRouter.cs`
- Modify: `src/NodiumGraph/Interactions/StraightRouter.cs`
- Modify: `src/NodiumGraph/Controls/ConnectionRenderer.cs` — update geometry branch
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — update `CuttingLineIntersectsGeometry`
- Test: `tests/NodiumGraph.Tests/ConnectionRendererTests.cs`

**Context:** `IsBezierRoute` is a boolean that couples the router to the renderer — the renderer special-cases `IsBezierRoute == true && Count == 4`. A `RouteKind` enum is more extensible and self-documenting.

- [ ] **Step 1: Create RouteKind enum**

Create `src/NodiumGraph/Interactions/RouteKind.cs`:

```csharp
namespace NodiumGraph.Interactions;

/// <summary>
/// Describes the type of path segments returned by an <see cref="IConnectionRouter"/>.
/// </summary>
public enum RouteKind
{
    /// <summary>Straight-line segments (polyline).</summary>
    Polyline,

    /// <summary>Cubic bezier curve (exactly 4 control points).</summary>
    Bezier
}
```

- [ ] **Step 2: Update IConnectionRouter**

Replace `bool IsBezierRoute` with:
```csharp
RouteKind RouteKind { get; }
```

- [ ] **Step 3: Update all router implementations**

`BezierRouter`: `public RouteKind RouteKind => RouteKind.Bezier;`
`StepRouter`: `public RouteKind RouteKind => RouteKind.Polyline;`
`StraightRouter`: `public RouteKind RouteKind => RouteKind.Polyline;`

- [ ] **Step 4: Update ConnectionRenderer and CuttingLineIntersectsGeometry**

Replace all `router.IsBezierRoute` with `router.RouteKind == RouteKind.Bezier`.
Replace all `ConnectionRouter.IsBezierRoute` with `ConnectionRouter.RouteKind == RouteKind.Bezier`.

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Interactions/RouteKind.cs src/NodiumGraph/Interactions/IConnectionRouter.cs src/NodiumGraph/Interactions/BezierRouter.cs src/NodiumGraph/Interactions/StepRouter.cs src/NodiumGraph/Interactions/StraightRouter.cs src/NodiumGraph/Controls/ConnectionRenderer.cs src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "refactor: replace IsBezierRoute bool with RouteKind enum on IConnectionRouter"
```

---

## Task Dependencies

```
Task 1 (Selection desync) ─── must complete before ──→ Task 4 (HashSet + batch)
Task 2 (Source port commit) ── independent
Task 3 (HitTest z-order)   ── independent
Task 5 (Pen caching)       ── independent
Task 6 (StreamGeometry)    ── independent
Task 7 (Minimap bounds)    ── independent
Task 8 (IDisposable)       ── independent
Task 9 (Cutting alloc)     ── independent (but if Task 10 lands first, use RouteKind)
Task 10 (RouteKind enum)   ── independent
```

Tasks 2, 3, 5, 6, 7, 8, 9, 10 are fully independent and can be parallelized.
Task 4 depends on Task 1 (Task 1 changes Select/Deselect, Task 4 adds HashSet backing to those same methods).

## Priority Order

| Order | Tasks | Theme |
|-------|-------|-------|
| 1st   | 1, 2, 3 | Correctness — selection desync, source port, hit-test |
| 2nd   | 4 | Scalability — O(1) selection, batch removal |
| 3rd   | 5, 6, 7, 9 | Performance — render allocation reduction |
| 4th   | 8 | Lifecycle — disposable cleanup |
| 5th   | 10 | API design — route kind enum |
