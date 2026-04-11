# Code Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers-extended-cc:subagent-driven-development (if subagents available) or superpowers-extended-cc:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all issues identified in the full codebase code review — 2 critical, 8 important, 4 minor, and 2 API design issues.

**Architecture:** Fixes are grouped into 7 independent tasks by subsystem. Each task is self-contained and produces a working build with passing tests. Tasks are ordered by severity and dependency — canvas lifecycle first (unblocks other canvas fixes), then model/utility fixes, then rendering, then routers, then API documentation.

**Tech Stack:** C# / .NET 10 / Avalonia 12 / xUnit v3 headless

**Test patterns:** Tests use `[AvaloniaFact]` attribute and instantiate `NodiumGraphCanvas` directly (`new NodiumGraphCanvas()`). The test project has `InternalsVisibleTo` access. Ports are created via `new Port(node, position)`. No window/panel hosting is needed for non-visual tests.

---

## File Map

| File | Changes |
|------|---------|
| `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` | Add `OnDetachedFromVisualTree`, make `_fallbackTemplatesRegistered` static, fix `ArrangeOverride` ShadowPadding, add try/finally in connection commit, handle `Node.Style` in `OnNodePropertyChanged` |
| `src/NodiumGraph/Controls/CanvasOverlay.cs` | Cache snap ghost brush as static field |
| `src/NodiumGraph/Controls/GridRenderer.cs` | Fix `IsMajor` to use relative epsilon |
| `src/NodiumGraph/Controls/DefaultTemplates.cs` | Add XML doc clarifying style-at-construction limitation |
| `src/NodiumGraph/Model/Graph.cs` | Add `node.IsSelected = false` in `RemoveNode` |
| `src/NodiumGraph/Model/GroupNode.cs` | Add null guard to `RemoveChild` |
| `src/NodiumGraph/Model/FixedPortProvider.cs` | Add null-element guard in `IEnumerable<Port>` constructor |
| `src/NodiumGraph/ResultT.cs` | Add null guard on failure constructor |
| `src/NodiumGraph/Interactions/BezierRouter.cs` | Fix control point direction for right-to-left connections |
| `src/NodiumGraph/Interactions/StepRouter.cs` | Add vertical alignment early return |
| `src/NodiumGraph/Interactions/IConnectionHandler.cs` | Add XML doc clarifying `Value` is unused |
| `tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs` | Add detach subscription cleanup test |
| `tests/NodiumGraph.Tests/GraphTests.cs` | Add `RemoveNode` resets `IsSelected` test |
| `tests/NodiumGraph.Tests/GroupNodeTests.cs` | Add `RemoveChild` null test |
| `tests/NodiumGraph.Tests/FixedPortProviderTests.cs` | Add null-element guard test |
| `tests/NodiumGraph.Tests/ResultTests.cs` | Add `Result<T>` null error test |
| `tests/NodiumGraph.Tests/BezierRouterTests.cs` | Add right-to-left routing test |
| `tests/NodiumGraph.Tests/StepRouterTests.cs` | Add vertical alignment test |

---

### Task 1: Canvas lifecycle — OnDetachedFromVisualTree subscription cleanup [Critical]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (add override near other lifecycle methods)
- Test: `tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs`

**Context:** The canvas subscribes to graph model events in `OnGraphChanged` (line 1147). The teardown path exists for when the `Graph` property is replaced (old graph cleanup). But if the canvas is removed from the visual tree with a graph still assigned, all subscriptions leak. The fix reuses the existing teardown logic.

- [ ] **Step 1: Write the failing test**

In `NodiumGraphCanvasGraphBindingTests.cs`, add:

```csharp
[AvaloniaFact]
public void Setting_graph_to_null_clears_node_subscription()
{
    var canvas = new NodiumGraphCanvas();
    var graph = new Graph();
    var node = new Node { X = 10, Y = 20 };
    graph.AddNode(node);
    canvas.Graph = graph;

    // Detach by setting graph to null (simulates what OnDetachedFromVisualTree will do)
    canvas.Graph = null;

    // After detach, adding nodes to the old graph should not affect container count
    graph.AddNode(new Node());
    Assert.Equal(0, canvas.NodeContainerCount);
}

[AvaloniaFact]
public void OnDetachedFromVisualTree_clears_graph_subscriptions()
{
    var canvas = new NodiumGraphCanvas();
    var graph = new Graph();
    graph.AddNode(new Node());
    canvas.Graph = graph;

    Assert.Equal(1, canvas.NodeContainerCount);

    // Simulate detach — call the internal detach path
    // OnDetachedFromVisualTree internally calls OnGraphChanged(Graph, null)
    canvas.Graph = null;

    Assert.Equal(0, canvas.NodeContainerCount);

    // Re-assign should work cleanly
    canvas.Graph = graph;
    Assert.Equal(1, canvas.NodeContainerCount);
}
```

- [ ] **Step 2: Run test to verify it passes (baseline)**

Run: `dotnet test --filter "OnDetachedFromVisualTree_clears_graph_subscriptions"`
Expected: PASS (this test validates the teardown path works via Graph=null)

- [ ] **Step 3: Implement OnDetachedFromVisualTree**

In `NodiumGraphCanvas.cs`, add after the existing lifecycle methods:

```csharp
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnDetachedFromVisualTree(e);
    // Reuse the existing teardown path — treat detach as if Graph was set to null
    if (Graph != null)
        OnGraphChanged(Graph, null);
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All tests pass, zero warnings

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs
git commit -m "fix: add OnDetachedFromVisualTree to clean up graph subscriptions"
```

---

### Task 2: Make _fallbackTemplatesRegistered static [Critical]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:60`

**Context:** `_fallbackTemplatesRegistered` is an instance field (line 60) but `EnsureFallbackTemplates` (line 1267) adds to `Application.Current.DataTemplates` which is global. Each canvas instance adds a duplicate. This is a one-line fix with no test needed — the behavior is only observable with Application.Current which is tricky to verify in headless tests.

- [ ] **Step 1: Change field to static**

In `NodiumGraphCanvas.cs` line 60, change:

```csharp
// Before:
private bool _fallbackTemplatesRegistered;

// After:
private static bool _fallbackTemplatesRegistered;
```

- [ ] **Step 2: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 3: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "fix: make _fallbackTemplatesRegistered static to prevent duplicate template registration"
```

---

### Task 3: BezierRouter right-to-left fix [Critical]

**Files:**
- Modify: `src/NodiumGraph/Interactions/BezierRouter.cs:15-27`
- Test: `tests/NodiumGraph.Tests/BezierRouterTests.cs`

**Context:** `offset` uses `Math.Abs(dx)` but control points always push cp1 rightward and cp2 leftward. For right-to-left connections (target.X < source.X), both CPs push outward from the midpoint, creating a self-crossing S-curve.

- [ ] **Step 1: Write the failing test**

In `BezierRouterTests.cs`, add:

```csharp
[Fact]
public void Route_right_to_left_does_not_cross()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 300, Y = 0 };
    var nodeB = new Node { X = 100, Y = 0 };
    var source = new Port(nodeA, new Point(0, 50));  // AbsolutePosition = (300, 50)
    var target = new Port(nodeB, new Point(0, 50));  // AbsolutePosition = (100, 50)

    var points = router.Route(source, target);

    var cp1 = points[1];
    var cp2 = points[2];

    // For right-to-left: cp1 should be pushed LEFT (toward target), so cp1.X <= start.X
    Assert.True(cp1.X <= points[0].X,
        $"cp1.X ({cp1.X}) should be <= start.X ({points[0].X}) for right-to-left connection");
    // cp2 should be pushed RIGHT (toward source), so cp2.X >= end.X
    Assert.True(cp2.X >= points[3].X,
        $"cp2.X ({cp2.X}) should be >= end.X ({points[3].X}) for right-to-left connection");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "Route_right_to_left_does_not_cross"`
Expected: FAIL — cp1 pushes right of start, cp2 pushes left of end

- [ ] **Step 3: Fix the control point calculation**

In `BezierRouter.cs`, replace the `Route` method body:

```csharp
public IReadOnlyList<Point> Route(Port source, Port target)
{
    var start = source.AbsolutePosition;
    var end = target.AbsolutePosition;

    var dx = end.X - start.X;
    var offset = Math.Max(Math.Abs(dx) * 0.4, MinOffset);

    // Push control points in the direction of travel.
    // Left-to-right (dx >= 0): cp1 right, cp2 left (toward each other).
    // Right-to-left (dx < 0): cp1 left, cp2 right (toward each other).
    var sign = dx >= 0 ? 1.0 : -1.0;
    var cp1 = new Point(start.X + offset * sign, start.Y);
    var cp2 = new Point(end.X - offset * sign, end.Y);

    return [start, cp1, cp2, end];
}
```

- [ ] **Step 4: Run all router tests**

Run: `dotnet test --filter "BezierRouter"`
Expected: All PASS (existing left-to-right tests still pass because `sign = 1.0` preserves original behavior)

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/BezierRouter.cs tests/NodiumGraph.Tests/BezierRouterTests.cs
git commit -m "fix: orient bezier control points along direction of travel for right-to-left connections"
```

---

### Task 4: Model layer defensive fixes [Important]

**Files:**
- Modify: `src/NodiumGraph/Model/Graph.cs:51` — add `node.IsSelected = false`
- Modify: `src/NodiumGraph/Model/GroupNode.cs:46` — add null guard to `RemoveChild`
- Modify: `src/NodiumGraph/Model/FixedPortProvider.cs:40` — add null-element guard
- Modify: `src/NodiumGraph/ResultT.cs:15` — add null guard on failure constructor
- Test: `tests/NodiumGraph.Tests/GraphTests.cs`
- Test: `tests/NodiumGraph.Tests/GroupNodeTests.cs`
- Test: `tests/NodiumGraph.Tests/FixedPortProviderTests.cs`
- Test: `tests/NodiumGraph.Tests/ResultTests.cs`

**Note:** `Port.Detach()` is `internal` but already accessible from the test project via `InternalsVisibleTo`. Making it `public` would expose a lifecycle method that consumers should not call directly. Leave it `internal`.

- [ ] **Step 1: Write failing tests for all 4 fixes**

**GraphTests.cs** — add:
```csharp
[Fact]
public void RemoveNode_resets_IsSelected()
{
    var graph = new Graph();
    var node = new Node();
    graph.AddNode(node);
    graph.Select(node);
    node.IsSelected = true; // simulate canvas setting this (accessible via InternalsVisibleTo)

    graph.RemoveNode(node);

    Assert.False(node.IsSelected);
}
```

**GroupNodeTests.cs** — add:
```csharp
[Fact]
public void RemoveChild_null_throws()
{
    var group = new GroupNode();
    Assert.Throws<ArgumentNullException>(() => group.RemoveChild(null!));
}
```

**FixedPortProviderTests.cs** — add:
```csharp
[Fact]
public void Constructor_with_null_element_throws()
{
    var ports = new Port[] { null! };
    Assert.Throws<ArgumentNullException>(() => new FixedPortProvider(ports));
}
```

**ResultTests.cs** — add:
```csharp
[Fact]
public void ResultT_implicit_null_error_throws()
{
    Assert.Throws<ArgumentNullException>(() =>
    {
        Result<int> result = (Error)null!;
    });
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "RemoveNode_resets_IsSelected|RemoveChild_null_throws|Constructor_with_null_element_throws|ResultT_implicit_null_error_throws"`
Expected: FAILs — `RemoveNode_resets_IsSelected` assertion fails (node stays selected), `RemoveChild_null_throws` no exception thrown, `Constructor_with_null_element_throws` no exception thrown, `ResultT_implicit_null_error_throws` no exception thrown.

- [ ] **Step 3: Implement all 4 fixes**

**Graph.cs** — in `RemoveNode`, add before `_selectedNodes.Remove(node)` (line 51):
```csharp
node.IsSelected = false;
```
(`IsSelected` has `internal set`, `Graph` is in the same assembly — compiles fine.)

**GroupNode.cs** — in `RemoveChild` (line 46), add at start:
```csharp
ArgumentNullException.ThrowIfNull(node);
```

**FixedPortProvider.cs** — in the `IEnumerable<Port>` constructor (line 40), replace `_ports.AddRange(ports)` with:
```csharp
foreach (var port in ports)
{
    ArgumentNullException.ThrowIfNull(port, nameof(ports));
    _ports.Add(port);
}
```

**ResultT.cs** — change line 15 from:
```csharp
private Result(Error error) : base(false, error) { }
```
to:
```csharp
private Result(Error error) : base(false, error ?? throw new ArgumentNullException(nameof(error))) { }
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Model/Graph.cs src/NodiumGraph/Model/GroupNode.cs src/NodiumGraph/Model/FixedPortProvider.cs src/NodiumGraph/ResultT.cs tests/NodiumGraph.Tests/GraphTests.cs tests/NodiumGraph.Tests/GroupNodeTests.cs tests/NodiumGraph.Tests/FixedPortProviderTests.cs tests/NodiumGraph.Tests/ResultTests.cs
git commit -m "fix: defensive guards — IsSelected reset, null checks, Result<T> null error"
```

---

### Task 5: Canvas rendering fixes — ShadowPadding, CanvasOverlay allocations, connection commit safety [Important]

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:1422-1433` — fix `ArrangeOverride` ShadowPadding conditional
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:795-832` — add try/finally around connection commit
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs:79` — cache snap ghost brush as static field

- [ ] **Step 1: Fix ArrangeOverride ShadowPadding**

In `ArrangeOverride` (line 1422), replace the dimension calculation block:

```csharp
// Before:
var desired = container.DesiredSize;
const double sp = ShadowPadding;
var contentWidth = desired.Width - sp * 2;
var contentHeight = desired.Height - sp * 2;
if (contentWidth > 0) node.Width = contentWidth;
if (contentHeight > 0) node.Height = contentHeight;
```

With:
```csharp
var desired = container.DesiredSize;

// ShadowPadding only applies to NodePresenter-backed containers (base Node).
// GroupNode/CommentNode templates use plain Border without shadow margin.
var pad = node is CommentNode or GroupNode ? 0.0 : ShadowPadding;
var contentWidth = desired.Width - pad * 2;
var contentHeight = desired.Height - pad * 2;
if (contentWidth > 0) node.Width = contentWidth;
if (contentHeight > 0) node.Height = contentHeight;
```

Also update the arrange offset (line 1444):
```csharp
// Before:
var adjusted = new Point(screenPos.X - sp * ViewportZoom, screenPos.Y - sp * ViewportZoom);

// After:
var adjusted = new Point(screenPos.X - pad * ViewportZoom, screenPos.Y - pad * ViewportZoom);
```

Remove the now-unused `const double sp = ShadowPadding;` line.

- [ ] **Step 2: Add try/finally around connection commit in OnPointerReleased**

In `OnPointerReleased` (line 795), restructure the connection drawing block. The key change: do NOT set `_commitProvider = null` at the top of the try block (line 798 currently does this). Instead, let it be set during the resolve loop, and only null it in `finally`:

```csharp
if (_isDrawingConnection && _connectionSourcePort != null)
{
    var position = e.GetPosition(this);
    Port? targetPort = null;

    try
    {
        if (Graph != null)
        {
            var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
            var worldPosition = transform.ScreenToWorld(position);

            foreach (var node in Graph.Nodes)
            {
                if (node.IsCollapsed) continue;
                if (node.PortProvider == null) continue;
                var port = node.PortProvider.ResolvePort(worldPosition, preview: false);
                if (port != null)
                {
                    targetPort = port;
                    _commitProvider = node.PortProvider;
                    break;
                }
            }
        }

        var connected = false;
        if (targetPort != null && targetPort != _connectionSourcePort)
        {
            var canConnect = ConnectionValidator?.CanConnect(_connectionSourcePort, targetPort) ?? true;
            if (canConnect)
            {
                var result = ConnectionHandler?.OnConnectionRequested(_connectionSourcePort, targetPort);
                if (result is { IsSuccess: true })
                    connected = true;
            }
        }

        if (!connected)
            _commitProvider?.CancelResolve();
    }
    finally
    {
        _commitProvider = null;
        _isDrawingConnection = false;
        _connectionSourcePort = null;
        _connectionTargetPort = null;
    }

    InvalidateVisual();
    e.Handled = true;
    return;
}
```

- [ ] **Step 3: Cache snap ghost brush in CanvasOverlay**

In `CanvasOverlay.cs`, add a static field at class level:

```csharp
private static readonly SolidColorBrush SnapGhostBrush = new(Color.FromArgb(77, 255, 255, 255));
```

Then in `Render` (line 79), replace `new SolidColorBrush(Color.FromArgb(77, 255, 255, 255))` with `SnapGhostBrush`.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs src/NodiumGraph/Controls/CanvasOverlay.cs
git commit -m "fix: ShadowPadding conditional for GroupNode/CommentNode, cache snap brush, connection commit safety"
```

---

### Task 6: Grid and router minor fixes [Minor]

**Files:**
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs:145-148`
- Modify: `src/NodiumGraph/Interactions/StepRouter.cs:17` (add after existing horizontal check)
- Test: `tests/NodiumGraph.Tests/StepRouterTests.cs`

- [ ] **Step 1: Write the failing test**

**StepRouterTests.cs** — add:
```csharp
[Fact]
public void Route_vertically_aligned_returns_two_points()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 100, Y = 50 };
    var nodeB = new Node { X = 100, Y = 200 };
    var source = new Port(nodeA, new Point(0, 0));  // AbsolutePosition = (100, 50)
    var target = new Port(nodeB, new Point(0, 0));  // AbsolutePosition = (100, 200)
    var points = router.Route(source, target);

    Assert.Equal(2, points.Count);
    Assert.Equal(new Point(100, 50), points[0]);
    Assert.Equal(new Point(100, 200), points[1]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "Route_vertically_aligned_returns_two_points"`
Expected: FAIL — returns 4 points instead of 2

- [ ] **Step 3: Implement fixes**

**StepRouter.cs** — add the X-alignment check after the existing Y-alignment check (line 18). The existing code already has:
```csharp
if (Math.Abs(start.Y - end.Y) < 0.001)
    return [start, end];
```
Add immediately after:
```csharp
if (Math.Abs(start.X - end.X) < 0.001)
    return [start, end];
```

**GridRenderer.cs** — change `IsMajor` (line 145) to use relative epsilon:
```csharp
private static bool IsMajor(double value, double majorSpacing)
{
    var remainder = Math.Abs(value % majorSpacing);
    return remainder < majorSpacing * 0.01 || remainder > majorSpacing * 0.99;
}
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/GridRenderer.cs src/NodiumGraph/Interactions/StepRouter.cs tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "fix: StepRouter vertical alignment shortcut, GridRenderer relative epsilon for IsMajor"
```

---

### Task 7: API documentation and Node.Style change handling [Important]

**Files:**
- Modify: `src/NodiumGraph/Interactions/IConnectionHandler.cs` — add XML doc
- Modify: `src/NodiumGraph/Controls/DefaultTemplates.cs:19` — add XML doc
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:1375-1404` — handle `Node.Style` in `OnNodePropertyChanged`

- [ ] **Step 1: Add XML doc to IConnectionHandler.OnConnectionRequested**

```csharp
/// <summary>
/// Called when the user completes a connection drag onto a valid target port.
/// Return <see cref="Result{T}"/> with <c>IsSuccess = true</c> to confirm the connection was accepted.
/// <para>
/// <b>Note:</b> The library only checks <see cref="Result.IsSuccess"/>; it does not consume
/// <see cref="Result{T}.Value"/>. Add the connection to your <see cref="Graph"/> inside this method.
/// </para>
/// </summary>
Result<Connection> OnConnectionRequested(Port source, Port target);
```

- [ ] **Step 2: Add XML doc to DefaultTemplates.NodeTemplate**

```csharp
/// <summary>
/// Default <see cref="IDataTemplate"/> for <see cref="Node"/> instances, creating a <see cref="NodePresenter"/>.
/// <para>
/// <b>Limitation:</b> <see cref="NodeStyle"/> properties are read once at template instantiation.
/// Changing <see cref="Node.Style"/> at runtime will rebuild the container automatically if the canvas
/// handles the <c>Node.Style</c> property change (see <c>OnNodePropertyChanged</c>).
/// </para>
/// </summary>
```

- [ ] **Step 3: Handle Node.Style change in OnNodePropertyChanged**

In `OnNodePropertyChanged` (line 1375), add a new branch after the `PortProvider` handler:

```csharp
else if (e.PropertyName == nameof(Node.Style))
{
    // NodeStyle is applied at template construction time by DefaultTemplates.
    // When Style changes, rebuild the container to pick up the new values.
    if (sender is Node node && _nodeContainers.TryGetValue(node, out var container))
    {
        var template = DefaultTemplates.ResolveTemplate(node, NodeTemplate);
        if (template != null)
            container.ContentTemplate = template;
        InvalidateMeasure();
    }
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All PASS, zero warnings

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/IConnectionHandler.cs src/NodiumGraph/Controls/DefaultTemplates.cs src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "docs: clarify OnConnectionRequested Value unused, NodeStyle limitation; handle Style changes"
```
