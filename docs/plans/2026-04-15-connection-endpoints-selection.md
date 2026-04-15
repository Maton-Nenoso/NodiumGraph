---
title: Connection endpoints & unified selection — implementation plan
tags: [plan]
status: active
created: 2026-04-15
updated: 2026-04-15
---

# Connection endpoint decorations & unified selection — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Ship per-end `IEndpointRenderer` decorations (arrow, diamond, circle, bar, none) on `IConnectionStyle` plus a unified `IGraphElement`-based selection model that lets users click, ctrl-click, marquee, and delete connections alongside nodes.

**Architecture:** Strategy interface for endpoint shapes (one implementation per shape, geometry-only, stateless). World-space connection geometry cache on `NodiumGraphCanvas` serves both rendering and hit-testing. Unified `Graph.SelectedItems` holds both nodes and connections; `SelectedNodes` and `SelectedConnections` become read-only mirror views. New `IGraphInteractionHandler` owns the batched delete path.

**Tech Stack:** C# / .NET 10 / Avalonia 12 / xUnit v3 / Avalonia headless for canvas interaction tests.

**Design reference:** `docs/plans/2026-04-15-connection-endpoints-selection-design.md`. Every task assumes familiarity with the design doc — read it first.

**Breaking changes (pre-1.0, no shims):**
- `IConnectionStyle` gains `SourceEndpoint` / `TargetEndpoint`
- `ISelectionHandler.OnSelectionChanged` param type → `IReadOnlyCollection<IGraphElement>`
- `INodeInteractionHandler.OnDeleteRequested` removed
- `Graph.SelectedNodes` becomes `ReadOnlyObservableCollection<Node>`
- `ConnectionRenderer.CreateGeometry` produces world-space geometry

**Conventions for every task below:**
- TDD: red test → run and confirm fail → implement → run and confirm pass → commit.
- Build + test suite runs clean at every commit.
- Avalonia 12 APIs verified via `mcp__avalonia-docs` MCP tools when in doubt.
- Each commit references the design doc in its body.

---

## Phase 1 — Endpoint renderer primitives

### Task 1: `IEndpointRenderer` interface + `NoneEndpoint` sentinel

**Files:**
- Create: `src/NodiumGraph/Interactions/IEndpointRenderer.cs`
- Create: `src/NodiumGraph/Interactions/NoneEndpoint.cs`
- Create: `tests/NodiumGraph.Tests/NoneEndpointTests.cs`

**Step 1: Write the failing test**

```csharp
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using Xunit;

public class NoneEndpointTests
{
    [Fact]
    public void GetInset_always_zero()
    {
        Assert.Equal(0, NoneEndpoint.Instance.GetInset(1));
        Assert.Equal(0, NoneEndpoint.Instance.GetInset(8));
    }

    [Fact]
    public void IsFilled_false() =>
        Assert.False(NoneEndpoint.Instance.IsFilled);

    [Fact]
    public void BuildGeometry_returns_empty()
    {
        var geo = NoneEndpoint.Instance.BuildGeometry(new Point(0, 0), new Vector(1, 0), 2);
        Assert.True(geo.Bounds.Width == 0 && geo.Bounds.Height == 0);
    }
}
```

**Step 2: Run test to confirm it fails**

Run: `dotnet test --filter FullyQualifiedName~NoneEndpointTests`
Expected: compile error (type does not exist).

**Step 3: Implement the interface and sentinel**

```csharp
// IEndpointRenderer.cs
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

public interface IEndpointRenderer
{
    double GetInset(double strokeThickness);
    bool IsFilled { get; }
    Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness);
}
```

```csharp
// NoneEndpoint.cs
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

public sealed class NoneEndpoint : IEndpointRenderer
{
    public static readonly NoneEndpoint Instance = new();
    private static readonly Geometry _empty = new StreamGeometry();
    private NoneEndpoint() { }
    public double GetInset(double strokeThickness) => 0;
    public bool IsFilled => false;
    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness) => _empty;
}
```

**Step 4: Run test to confirm it passes**

Run: `dotnet test --filter FullyQualifiedName~NoneEndpointTests`
Expected: 3 passed.

**Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/IEndpointRenderer.cs src/NodiumGraph/Interactions/NoneEndpoint.cs tests/NodiumGraph.Tests/NoneEndpointTests.cs
git commit -m "feat(connections): add IEndpointRenderer interface and NoneEndpoint sentinel"
```

---

### Task 2: `ArrowEndpoint`

**Files:**
- Create: `src/NodiumGraph/Interactions/ArrowEndpoint.cs`
- Create: `tests/NodiumGraph.Tests/ArrowEndpointTests.cs`

**Step 1: Write the failing tests**

```csharp
public class ArrowEndpointTests
{
    [Fact]
    public void GetInset_filled_equals_size()
        => Assert.Equal(8, new ArrowEndpoint(size: 8, filled: true).GetInset(2));

    [Fact]
    public void GetInset_open_is_90_percent_of_size()
        => Assert.Equal(7.2, new ArrowEndpoint(size: 8, filled: false).GetInset(2), 3);

    [Theory]
    [InlineData(1, 0)]     // pointing +X
    [InlineData(0, 1)]     // pointing +Y
    [InlineData(-1, 0)]    // pointing -X
    [InlineData(0, -1)]    // pointing -Y
    public void BuildGeometry_tip_at_expected_point(double dx, double dy)
    {
        var arrow = new ArrowEndpoint(size: 10, filled: true);
        var tip = new Point(50, 50);
        var geo = arrow.BuildGeometry(tip, new Vector(dx, dy), strokeThickness: 2);
        var bounds = geo.Bounds;
        Assert.Contains(tip, rect => rect.Contains(tip)); // placeholder
        Assert.True(bounds.Contains(tip) || bounds.Inflate(0.5).Contains(tip));
    }

    [Fact]
    public void IsFilled_reflects_constructor()
    {
        Assert.True(new ArrowEndpoint(size: 8, filled: true).IsFilled);
        Assert.False(new ArrowEndpoint(size: 8, filled: false).IsFilled);
    }
}
```

**Step 2: Run test to confirm it fails**

Run: `dotnet test --filter FullyQualifiedName~ArrowEndpointTests`
Expected: compile error.

**Step 3: Implement**

```csharp
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Interactions;

public sealed class ArrowEndpoint : IEndpointRenderer
{
    private readonly double _size;

    public bool IsFilled { get; }

    public ArrowEndpoint(double size = 8, bool filled = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        _size = size;
        IsFilled = filled;
    }

    public double GetInset(double strokeThickness) => IsFilled ? _size : _size * 0.9;

    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
    {
        // Canonical geometry points LEFT from origin (direction (1,0) pointing into tip).
        // Base-left = (-size, -size/2), base-right = (-size, +size/2).
        var half = _size / 2.0;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, 0), IsFilled);
            ctx.LineTo(new Point(-_size, -half));
            ctx.LineTo(new Point(-_size, half));
            ctx.EndFigure(IsFilled);
        }

        var m = Matrix.CreateRotation(System.Math.Atan2(direction.Y, direction.X))
              * Matrix.CreateTranslation(tip.X, tip.Y);
        geo.Transform = new MatrixTransform(m);
        return geo;
    }
}
```

**Step 4: Run test to confirm it passes**

Run: `dotnet test --filter FullyQualifiedName~ArrowEndpointTests`
Expected: all pass. Fix placeholder assertion to check that `geo.Bounds` (after transform) has `tip` as one of its extremes per direction.

**Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/ArrowEndpoint.cs tests/NodiumGraph.Tests/ArrowEndpointTests.cs
git commit -m "feat(connections): add ArrowEndpoint renderer"
```

---

### Task 3: `DiamondEndpoint`, `CircleEndpoint`, `BarEndpoint`

One class each, following the `ArrowEndpoint` template. Each gets its own file under `src/NodiumGraph/Interactions/` and its own test file under `tests/NodiumGraph.Tests/`.

**Geometry spec** (canonical orientation: direction `(1,0)`, tip at origin):

- **DiamondEndpoint(size, filled):** rhombus through `(0,0)`, `(-size/2, size/2)`, `(-size, 0)`, `(-size/2, -size/2)`. Inset = `size`.
- **CircleEndpoint(radius, filled):** `EllipseGeometry` at `(-radius, 0)` with `radius`. Inset = `radius * 2`. Use `EllipseGeometry`, not `StreamGeometry`.
- **BarEndpoint(width):** single line from `(-width/2, -size/2)` to `(-width/2, size/2)` where `size = Math.Max(width * 3, strokeThickness * 4)`. `IsFilled` always false. Inset = `width / 2`.

**Step 1: Write one test per class** (3 small test files).

**Step 2: Run to confirm red**

Run: `dotnet test --filter FullyQualifiedName~DiamondEndpointTests|FullyQualifiedName~CircleEndpointTests|FullyQualifiedName~BarEndpointTests`

**Step 3: Implement all three.** Copy `ArrowEndpoint` shape, swap the canonical geometry and inset.

**Step 4: Run tests, confirm green.**

**Step 5: Commit in one commit**

```bash
git add src/NodiumGraph/Interactions/DiamondEndpoint.cs src/NodiumGraph/Interactions/CircleEndpoint.cs src/NodiumGraph/Interactions/BarEndpoint.cs tests/NodiumGraph.Tests/DiamondEndpointTests.cs tests/NodiumGraph.Tests/CircleEndpointTests.cs tests/NodiumGraph.Tests/BarEndpointTests.cs
git commit -m "feat(connections): add Diamond, Circle, Bar endpoint renderers"
```

---

### Task 4: `RouteTangents` helper

**Files:**
- Create: `src/NodiumGraph/Controls/RouteTangents.cs`
- Create: `tests/NodiumGraph.Tests/RouteTangentsTests.cs`

**Step 1: Write the failing tests**

```csharp
public class RouteTangentsTests
{
    [Fact]
    public void Bezier_4_points_uses_cp_tangents()
    {
        var points = new[]
        {
            new Point(0, 0), new Point(10, 0),
            new Point(20, 10), new Point(30, 10)
        };
        var t = RouteTangents.From(points, RouteKind.Bezier);
        Assert.Equal(new Vector(1, 0), Normalize(t.Source));
        Assert.Equal(Normalize(new Vector(10, 0)), Normalize(t.Target));
    }

    [Fact]
    public void Polyline_uses_first_and_last_segments()
    {
        var points = new[] { new Point(0, 0), new Point(10, 0), new Point(10, 10) };
        var t = RouteTangents.From(points, RouteKind.Polyline);
        Assert.Equal(new Vector(1, 0), Normalize(t.Source));
        Assert.Equal(new Vector(0, 1), Normalize(t.Target));
    }

    [Fact]
    public void Empty_or_degenerate_returns_zero()
    {
        var t = RouteTangents.From(new[] { new Point(5, 5) }, RouteKind.Polyline);
        Assert.Equal(default, t.Source);
        Assert.Equal(default, t.Target);
    }

    private static Vector Normalize(Vector v) => v / v.Length;
}
```

**Step 2: Run, confirm red.** `dotnet test --filter FullyQualifiedName~RouteTangentsTests`

**Step 3: Implement**

```csharp
using System.Collections.Generic;
using Avalonia;
using NodiumGraph.Interactions;

namespace NodiumGraph.Controls;

internal readonly record struct RouteTangents(Vector Source, Vector Target)
{
    public static RouteTangents From(IReadOnlyList<Point> points, RouteKind kind)
    {
        if (points is null || points.Count < 2)
            return default;

        if (kind == RouteKind.Bezier && points.Count == 4)
            return new RouteTangents(
                Normalize(points[1] - points[0]),
                Normalize(points[3] - points[2]));

        return new RouteTangents(
            Normalize(points[1] - points[0]),
            Normalize(points[^1] - points[^2]));
    }

    private static Vector Normalize(Vector v)
    {
        var len = v.Length;
        return len < 1e-9 ? default : v / len;
    }
}
```

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/RouteTangents.cs tests/NodiumGraph.Tests/RouteTangentsTests.cs
git commit -m "feat(connections): add RouteTangents helper for endpoint direction"
```

---

## Phase 2 — ConnectionStyle endpoint properties

### Task 5: Extend `IConnectionStyle` and `ConnectionStyle`

**Files:**
- Modify: `src/NodiumGraph/Interactions/IConnectionStyle.cs`
- Modify: `src/NodiumGraph/Interactions/ConnectionStyle.cs`
- Modify: `tests/NodiumGraph.Tests/ConnectionStyleTests.cs`

**Step 1: Extend existing tests**

Add to `ConnectionStyleTests`:

```csharp
[Fact]
public void Default_endpoints_are_null()
{
    var style = new ConnectionStyle();
    Assert.Null(style.SourceEndpoint);
    Assert.Null(style.TargetEndpoint);
}

[Fact]
public void Endpoints_round_trip_through_constructor()
{
    var arrow = new ArrowEndpoint();
    var style = new ConnectionStyle(targetEndpoint: arrow);
    Assert.Same(arrow, style.TargetEndpoint);
    Assert.Null(style.SourceEndpoint);
}
```

**Step 2: Run, confirm red.**

**Step 3: Update the interface and class**

```csharp
// IConnectionStyle.cs — add two members
IEndpointRenderer? SourceEndpoint { get; }
IEndpointRenderer? TargetEndpoint { get; }
```

```csharp
// ConnectionStyle.cs
public class ConnectionStyle : IConnectionStyle
{
    public IBrush Stroke { get; }
    public double Thickness { get; }
    public IDashStyle? DashPattern { get; }
    public IEndpointRenderer? SourceEndpoint { get; }
    public IEndpointRenderer? TargetEndpoint { get; }

    public ConnectionStyle(
        IBrush? stroke = null,
        double thickness = 2.0,
        IDashStyle? dashPattern = null,
        IEndpointRenderer? sourceEndpoint = null,
        IEndpointRenderer? targetEndpoint = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thickness);
        Stroke = stroke ?? Brushes.Gray;
        Thickness = thickness;
        DashPattern = dashPattern;
        SourceEndpoint = sourceEndpoint;
        TargetEndpoint = targetEndpoint;
    }
}
```

**Step 4: Run, confirm green** (existing `ConnectionStyleTests` still pass — endpoints default to null).

**Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/IConnectionStyle.cs src/NodiumGraph/Interactions/ConnectionStyle.cs tests/NodiumGraph.Tests/ConnectionStyleTests.cs
git commit -m "feat(connections): add per-end endpoint renderers to IConnectionStyle"
```

---

## Phase 3 — ConnectionRenderer rework

### Task 6: World-space geometry switch

**Files:**
- Modify: `src/NodiumGraph/Controls/ConnectionRenderer.cs`
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (single call site)
- Modify: `tests/NodiumGraph.Tests/ConnectionRendererTests.cs`

**Goal:** `CreateGeometry` produces **world-space** geometry. `Render` wraps `DrawGeometry` in `context.PushTransform(viewport)`. No endpoint logic yet — this is a pure refactor with regression gates.

**Step 1: Write/update the regression test**

Add to `ConnectionRendererTests`:

```csharp
[Fact]
public void CreateGeometry_is_in_world_space()
{
    var geo = ConnectionRenderer.CreateGeometry(connection, router, transform);
    // Expected bounds in world coordinates, not multiplied by zoom or offset.
    var expected = /* world-space bounding box */;
    Assert.Equal(expected, geo.Bounds);
}
```

Keep the existing "geometry has expected point count / shape" tests; update them to expect world coordinates.

**Step 2: Run, confirm red.**

**Step 3: Implement**

`ConnectionRenderer.CreateGeometry`:
- Remove all `transform.WorldToScreen(...)` calls.
- Points go in raw from `router.Route`.
- Return value is a world-space `Geometry` (still wrapped in a `StreamGeometry`).

`ConnectionRenderer.Render`:
- Push the viewport transform via `context.PushTransform(transform.GetWorldToScreenMatrix())` (or equivalent — verify name via `mcp__avalonia-docs__lookup_avalonia_api`).
- Draw inside the push scope.
- Pop (via `using var _ = ...`).

`NodiumGraphCanvas` connection render loop: push the viewport transform **once**, render all connections inside, then pop. Eliminates redundant push/pop per connection.

**Step 4: Run the full suite, confirm green.**

Manual smoke: run the sample app and check that pan and zoom still render connections correctly.

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/ConnectionRenderer.cs src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/ConnectionRendererTests.cs
git commit -m "refactor(connections): build geometry in world space, push viewport at render"
```

---

### Task 7: Endpoint integration in `ConnectionRenderer`

**Files:**
- Modify: `src/NodiumGraph/Controls/ConnectionRenderer.cs`
- Modify: `tests/NodiumGraph.Tests/ConnectionRendererTests.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void Endpoint_geometry_is_added_to_group()
{
    var style = new ConnectionStyle(targetEndpoint: new ArrowEndpoint(size: 8));
    var geo = ConnectionRenderer.CreateGeometry(connection, router, style);
    var group = Assert.IsType<GeometryGroup>(geo);
    Assert.Equal(2, group.Children.Count); // stroke + target endpoint
}

[Fact]
public void Line_is_inset_by_endpoint_inset()
{
    var style = new ConnectionStyle(targetEndpoint: new ArrowEndpoint(size: 10));
    // Build and inspect stroke end position; assert it's 10 units before the port center along the tangent.
}

[Fact]
public void NoneEndpoint_preserves_today_output()
{
    // Two calls: one with NoneEndpoint target, one with null target.
    // Bounds and point counts should match.
}
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

Update `CreateGeometry(Connection, IConnectionRouter, IConnectionStyle)`:

```csharp
public static Geometry CreateGeometry(
    Connection connection, IConnectionRouter router, IConnectionStyle style)
{
    var points = router.Route(connection.SourcePort, connection.TargetPort);
    if (points.Count < 2)
        return new StreamGeometry();

    var tangents = RouteTangents.From(points, router.RouteKind);
    var sourceInset = style.SourceEndpoint?.GetInset(style.Thickness) ?? 0;
    var targetInset = style.TargetEndpoint?.GetInset(style.Thickness) ?? 0;

    // Apply inset: shift p0 outward by -sourceInset * sourceTangent,
    // shift last point outward by -targetInset * targetTangent.
    var insetPoints = ApplyInsets(points, tangents, sourceInset, targetInset, router.RouteKind);

    var stroke = BuildStrokeGeometry(insetPoints, router.RouteKind);

    if (style.SourceEndpoint is null && style.TargetEndpoint is null)
        return stroke;

    var group = new GeometryGroup { FillRule = FillRule.NonZero };
    group.Children.Add(stroke);
    if (style.SourceEndpoint is not null && tangents.Source != default)
        group.Children.Add(style.SourceEndpoint.BuildGeometry(
            points[0], -tangents.Source, style.Thickness));
    if (style.TargetEndpoint is not null && tangents.Target != default)
        group.Children.Add(style.TargetEndpoint.BuildGeometry(
            points[^1], tangents.Target, style.Thickness));
    return group;
}
```

Extract helpers:
- `ApplyInsets(points, tangents, sourceInset, targetInset, kind)` → new points array with endpoints shortened.
- `BuildStrokeGeometry(points, kind)` → `StreamGeometry` (what `CreateGeometry` does today, minus the world→screen baking).

Update `Render` to draw endpoint groups bucketed by `IsFilled`:

```csharp
public static void Render(DrawingContext context, Connection connection,
    IConnectionRouter router, IConnectionStyle style, Pen strokePen,
    ViewportTransform transform, bool selected, Pen? haloPen)
{
    var geometry = CreateGeometry(connection, router, style);
    if (selected && haloPen is not null)
        context.DrawGeometry(null, haloPen, geometry);
    context.DrawGeometry(null, strokePen, /* stroke child only */);
    // filled endpoints: DrawGeometry(strokeBrush, strokePen, filledGroup)
    // open endpoints:   DrawGeometry(null,         strokePen, openGroup)
}
```

Note: the filled-vs-open bucketing needs the `CreateGeometry` output structured so the caller can separate children. Two options: (a) return a small record `(Geometry stroke, Geometry? filled, Geometry? open)` instead of a `GeometryGroup`, (b) keep `GeometryGroup` for caching but add a separate `CreateRenderable` method. **Pick (a) for render, keep a `CreateHitTestGeometry` that returns the combined `GeometryGroup` for hit-testing.** The stroke geometry is shared between both.

Final method surface on `ConnectionRenderer`:
- `Geometry CreateHitTestGeometry(Connection, IConnectionRouter, IConnectionStyle)` — `GeometryGroup { stroke, filled-endpoints, open-endpoints }` in world space. Used for both rendering composition and hit-testing.
- `ConnectionRenderable CreateRenderable(Connection, IConnectionRouter, IConnectionStyle)` — returns `(Geometry stroke, Geometry? filledEndpoints, Geometry? openEndpoints, Rect worldBounds)`.
- `void Render(DrawingContext, ConnectionRenderable, Pen stroke, Pen? halo)` — draws in world space (caller pushes viewport).

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/ConnectionRenderer.cs tests/NodiumGraph.Tests/ConnectionRendererTests.cs
git commit -m "feat(connections): render endpoint decorations with inset stroke"
```

---

### Task 8: Selection halo pass

**Files:**
- Modify: `src/NodiumGraph/Controls/ConnectionRenderer.cs`
- Modify: `src/NodiumGraph/Themes/Generic.axaml`
- Modify: `src/NodiumGraph/NodiumGraphResources.cs`
- Modify: `tests/NodiumGraph.Tests/ConnectionRendererTests.cs`

**Step 1: Add a test that exercises halo draw ordering**

Use a recording `DrawingContext` (see existing canvas tests for the pattern) to assert: if `selected` is true, a halo `DrawGeometry` call happens before the stroke draw call, using the halo brush from theme resources.

**Step 2: Run, confirm red.**

**Step 3: Implement**

- Add `ConnectionSelectionHaloBrushKey` constant to `NodiumGraphResources`.
- Add the brush to `Generic.axaml` under both Light and Dark theme dictionaries (low-alpha accent color).
- Update `ConnectionRenderer.Render` to take `Pen? haloPen` and draw it first when non-null.
- `NodiumGraphCanvas` resolves the halo brush via `GetOrCreateHaloPen()` (new single-slot pen helper following the existing `GetOrCreateSelectedBorderPen` pattern), passes to `Render` per connection.

**Step 4: Run, confirm green.**

Manual smoke: run sample, select a connection (via temporary test wiring), confirm halo is visible and re-themes correctly.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(connections): render selection halo as under-stroke pass"
```

---

## Phase 4 — IGraphElement + unified selection

### Task 9: `IGraphElement` marker + `Node`/`Connection` implement

**Files:**
- Create: `src/NodiumGraph/Model/IGraphElement.cs`
- Modify: `src/NodiumGraph/Model/Node.cs`
- Modify: `src/NodiumGraph/Model/Connection.cs`
- Create: `tests/NodiumGraph.Tests/IGraphElementTests.cs`

**Step 1: Test**

```csharp
[Fact]
public void Node_and_Connection_are_IGraphElement()
{
    Assert.IsAssignableFrom<IGraphElement>(new Node());
    // Connection requires two ports — use test helper.
    Assert.IsAssignableFrom<IGraphElement>(TestGraph.CreateSimpleConnection());
}
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

```csharp
namespace NodiumGraph.Model;
public interface IGraphElement { }
```

Add `: IGraphElement` to `Node` and `Connection` (alongside existing bases / interfaces).

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(model): add IGraphElement marker interface for Node and Connection"
```

---

### Task 10: `Graph.SelectedItems` canonical + mirror views

**Files:**
- Modify: `src/NodiumGraph/Model/Graph.cs`
- Modify: `tests/NodiumGraph.Tests/GraphTests.cs`

**Step 1: Tests**

```csharp
[Fact]
public void Adding_node_to_SelectedItems_appears_in_SelectedNodes_view()
{
    var graph = new Graph();
    var node = new Node(); graph.AddNode(node);
    graph.SelectedItems.Add(node);
    Assert.Contains(node, graph.SelectedNodes);
    Assert.Empty(graph.SelectedConnections);
}

[Fact]
public void Adding_connection_fires_SelectedConnections_event()
{
    // Arrange: subscribe to ((INotifyCollectionChanged)graph.SelectedConnections).CollectionChanged.
    // Act: graph.SelectedItems.Add(conn).
    // Assert: event fired with NewItems containing conn.
}

[Fact]
public void Mixed_selection_partitions_into_views()
{
    graph.SelectedItems.Add(node);
    graph.SelectedItems.Add(connection);
    Assert.Single(graph.SelectedNodes);
    Assert.Single(graph.SelectedConnections);
}
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

```csharp
public class Graph
{
    private readonly ObservableCollection<Node> _selectedNodes = new();
    private readonly ObservableCollection<Connection> _selectedConnections = new();

    public ObservableCollection<IGraphElement> SelectedItems { get; }
    public ReadOnlyObservableCollection<Node> SelectedNodes { get; }
    public ReadOnlyObservableCollection<Connection> SelectedConnections { get; }

    public Graph()
    {
        // existing Nodes, Connections init
        SelectedItems = new ObservableCollection<IGraphElement>();
        SelectedNodes = new ReadOnlyObservableCollection<Node>(_selectedNodes);
        SelectedConnections = new ReadOnlyObservableCollection<Connection>(_selectedConnections);
        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }

    private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Maintain _selectedNodes and _selectedConnections based on e.Action + e.NewItems + e.OldItems.
        // Reset action triggers full rebuild.
    }
}
```

**Step 4: Run, confirm green. All existing `SelectedNodes` consumers still work because the view is still an `ObservableCollection`-compatible type.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(model): add unified Graph.SelectedItems with filtered views"
```

---

### Task 11: Remove-cascade into `SelectedItems`

**Files:**
- Modify: `src/NodiumGraph/Model/Graph.cs`
- Modify: `tests/NodiumGraph.Tests/GraphTests.cs`

**Step 1: Tests**

```csharp
[Fact]
public void RemoveNode_removes_from_SelectedItems()
{
    graph.SelectedItems.Add(node);
    graph.RemoveNode(node);
    Assert.DoesNotContain(node, graph.SelectedItems);
}

[Fact]
public void RemoveNode_cascade_removes_connections_from_SelectedItems()
{
    // Node with two connections, both selected plus the node itself.
    // RemoveNode → all three gone from SelectedItems.
}

[Fact]
public void RemoveConnection_removes_from_SelectedItems()
{
    graph.SelectedItems.Add(connection);
    graph.RemoveConnection(connection);
    Assert.DoesNotContain(connection, graph.SelectedItems);
}
```

**Step 2: Run, confirm red.**

**Step 3: Implement** — update `Graph.RemoveNode` and `Graph.RemoveConnection` to `SelectedItems.Remove(...)` before returning.

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(model): cascade selection removal on RemoveNode/RemoveConnection"
```

---

## Phase 5 — Hit-testing

### Task 12: Connection geometry cache on canvas

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create or modify: a small internal types file for `CachedConnectionGeometry` (can colocate in `ConnectionRenderer.cs`)

**Step 1: Add a test**

Use an `InternalsVisibleTo`-enabled test that constructs a canvas with a headless app, adds a connection, forces a render, and asserts the cache contains an entry keyed by connection ID with non-empty bounds.

**Step 2: Run, confirm red.**

**Step 3: Implement**

- Add `internal readonly record struct CachedConnectionGeometry(Geometry Stroke, Geometry HitTestShape, Rect WorldBounds, int Version);`.
- Add `_connectionGeometryCache = new Dictionary<Guid, CachedConnectionGeometry>();` field on the canvas.
- During connection render pass, compute (or fetch) the cached entry, draw using it. Miss → build + insert. Hit → reuse.
- Add `_connectionRouteVersion` counter per connection (increment on invalidation).

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(canvas): cache connection geometry per connection for hit-testing"
```

---

### Task 13: `ConnectionHitTester.HitTest`

**Files:**
- Create: `src/NodiumGraph/Controls/ConnectionHitTester.cs`
- Create: `tests/NodiumGraph.Tests/ConnectionHitTesterTests.cs`

**Step 1: Tests**

```csharp
[Fact]
public void Click_on_line_returns_connection() { /* ... */ }

[Fact]
public void Click_beyond_tolerance_returns_null() { /* ... */ }

[Fact]
public void Click_on_filled_arrow_interior_hits_via_FillContains() { /* ... */ }

[Fact]
public void Overlapping_connections_topmost_wins() { /* ... */ }

[Fact]
public void Tolerance_scales_with_zoom()
{
    // At zoom 0.5, world tolerance is 16; at zoom 2, it's 4.
}
```

Tests should build small in-memory caches directly — no canvas required.

**Step 2: Run, confirm red.**

**Step 3: Implement**

```csharp
namespace NodiumGraph.Controls;

internal static class ConnectionHitTester
{
    public static Connection? HitTest(
        Point worldPoint,
        double worldTolerance,
        IReadOnlyList<Connection> connections,
        Func<Connection, IConnectionStyle> resolveStyle,
        IReadOnlyDictionary<Guid, CachedConnectionGeometry> cache)
    {
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            var c = connections[i];
            if (!cache.TryGetValue(c.Id, out var entry)) continue;
            if (!entry.WorldBounds.Inflate(worldTolerance).Contains(worldPoint)) continue;

            var style = resolveStyle(c);
            var hitThickness = Math.Max(style.Thickness, worldTolerance);
            var hitPen = new Pen(Brushes.Black, hitThickness);

            if (entry.HitTestShape.StrokeContains(hitPen, worldPoint)
                || entry.HitTestShape.FillContains(worldPoint))
                return c;
        }
        return null;
    }
}
```

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(canvas): add ConnectionHitTester with StrokeContains+FillContains"
```

---

### Task 14: Cache invalidation wiring

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`

Invalidation triggers to wire up (each is a single subscription / override):

- Connection added to `Graph.Connections` → nothing proactive; next render will populate.
- Connection removed → `_connectionGeometryCache.Remove(c.Id)`.
- Node X/Y changed (existing listener) → iterate its connections, increment route version → cache miss next render.
- Router swapped (`ConnectionRouter` styled property changed) → `_connectionGeometryCache.Clear()`.
- Connection style changed (per-connection via `IConnectionStyleProvider` or default style changed) → invalidate affected.

**Step 1: Tests** — headless canvas tests asserting cache state after each trigger.

**Step 2: Run, confirm red.**

**Step 3: Implement each trigger.**

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(canvas): invalidate connection geometry cache on model changes"
```

---

## Phase 6 — Handler surface changes

### Task 15: `ISelectionHandler.OnSelectionChanged` signature

**Files:**
- Modify: `src/NodiumGraph/Interactions/ISelectionHandler.cs`
- Modify: `samples/NodiumGraph.Sample/` + `samples/GettingStarted/` handler implementations
- Modify: all test fakes that implement `ISelectionHandler`

**Step 1: Update tests to use `IReadOnlyCollection<IGraphElement>`.**

**Step 2: Build — confirm compile errors at every consumer site (expected red state via build).**

**Step 3: Update every consumer:**
- Interface definition: `void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected);`
- Sample `SampleSelectionHandler`: `.OfType<Node>().ToList()` at the top, then existing logic.
- Test fakes: same pattern.

**Step 4: Build + test green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "refactor(handlers): ISelectionHandler.OnSelectionChanged takes IGraphElement"
```

---

### Task 16: Remove `INodeInteractionHandler.OnDeleteRequested`

**Files:**
- Modify: `src/NodiumGraph/Interactions/INodeInteractionHandler.cs`
- Modify: every consumer (sample app, tests)

**Step 1: Delete the method from the interface.**

**Step 2: Build — observe consumer breaks.**

**Step 3: Delete the corresponding implementations in sample and test fakes. Do NOT re-add under a new name here — that comes in Task 17.**

**Step 4: Build — green (delete handling is temporarily disabled end-to-end).**

**Step 5: Commit**

```bash
git add -A
git commit -m "refactor(handlers): remove INodeInteractionHandler.OnDeleteRequested"
```

---

### Task 17: `IGraphInteractionHandler` + canvas styled property

**Files:**
- Create: `src/NodiumGraph/Interactions/IGraphInteractionHandler.cs`
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (new styled property)
- Modify: `tests/NodiumGraph.Tests/NodiumGraphCanvasTests.cs`

**Step 1: Tests**

```csharp
[Fact]
public void GraphInteractionHandler_styled_property_roundtrips() { /* ... */ }
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

```csharp
public interface IGraphInteractionHandler
{
    void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements);
}
```

```csharp
// In NodiumGraphCanvas
public static readonly StyledProperty<IGraphInteractionHandler?> GraphInteractionHandlerProperty =
    AvaloniaProperty.Register<NodiumGraphCanvas, IGraphInteractionHandler?>(nameof(GraphInteractionHandler));

public IGraphInteractionHandler? GraphInteractionHandler
{
    get => GetValue(GraphInteractionHandlerProperty);
    set => SetValue(GraphInteractionHandlerProperty, value);
}
```

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(handlers): add IGraphInteractionHandler for unified delete"
```

---

## Phase 7 — Canvas interaction wiring

### Task 18: Pointer-press connection hit-test path

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Modify: `tests/NodiumGraph.Tests/NodiumGraphCanvasTests.cs`

**Step 1: Tests**

```csharp
[Fact]
public void Click_on_connection_replaces_selection()
{
    // Setup canvas + handler spy.
    // Simulate pointer press at a point on the connection.
    // Assert SelectedItems == { connection } and ISelectionHandler fired once.
}

[Fact]
public void Click_on_empty_canvas_clears_selection() { /* ... */ }

[Fact]
public void Click_on_port_does_not_select_connection()
{
    // Port overlapping a connection line; click should start connection draw, not select.
}
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

In `OnPointerPressed` (after existing port/node hit-test):

```csharp
// After port and node rejection
var worldPoint = _transform.ScreenToWorld(e.GetPosition(this));
var worldTolerance = 8.0 / ViewportZoom;
var hit = ConnectionHitTester.HitTest(
    worldPoint, worldTolerance,
    Graph.Connections, ResolveConnectionStyle, _connectionGeometryCache);
if (hit is not null)
{
    if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        Graph.SelectedItems.Clear();
    if (Graph.SelectedItems.Contains(hit))
        Graph.SelectedItems.Remove(hit);
    else
        Graph.SelectedItems.Add(hit);
    SelectionHandler?.OnSelectionChanged(Graph.SelectedItems);
    e.Handled = true;
    return;
}
```

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(canvas): click and ctrl-click selection for connections"
```

---

### Task 19: Marquee picks connections

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Modify: `src/NodiumGraph/Controls/GeometryHelpers.cs` (or whichever file has intersection helpers)
- Modify: `tests/NodiumGraph.Tests/MarqueeSelectionTests.cs` (extend existing or create)

**Step 1: Tests**

```csharp
[Fact]
public void Marquee_across_connection_bounding_box_selects_it() { /* ... */ }

[Fact]
public void Ctrl_marquee_is_additive_across_mixed_types() { /* ... */ }
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

- In the existing marquee commit path, after gathering nodes whose rects intersect, iterate cached connection geometries:
  - `WorldBounds.Intersects(marqueeRect)` cheap reject
  - If the connection stroke / endpoint geometry intersects `marqueeRect` via a segment-intersection helper (reuse any existing helpers from `GeometryHelpers`), add to the batch.
- Build a single `Reset`-action `CollectionChanged` event by swapping `SelectedItems` contents in one batch (inside a `BatchUpdate` or simply `Clear` + `AddRange` with a single event — check how node multi-select already batches).

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(canvas): marquee selection picks connections by intersection"
```

---

### Task 20: Delete key → `IGraphInteractionHandler.OnDeleteRequested`

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Modify: `tests/NodiumGraph.Tests/NodiumGraphCanvasTests.cs`

**Step 1: Tests**

```csharp
[Fact]
public void Delete_key_fires_IGraphInteractionHandler_with_SelectedItems()
{
    var spy = new GraphInteractionHandlerSpy();
    canvas.GraphInteractionHandler = spy;
    canvas.Graph.SelectedItems.Add(node);
    canvas.Graph.SelectedItems.Add(connection);
    SimulateKey(canvas, Key.Delete);
    Assert.Equal(new IGraphElement[] { node, connection }, spy.LastDeleted);
}

[Fact]
public void Delete_key_with_empty_selection_noop() { /* ... */ }
```

**Step 2: Run, confirm red.**

**Step 3: Implement**

In `OnKeyDown`:

```csharp
if (e.Key == Key.Delete && Graph.SelectedItems.Count > 0)
{
    var batch = Graph.SelectedItems.ToList();
    GraphInteractionHandler?.OnDeleteRequested(batch);
    e.Handled = true;
}
```

**Step 4: Run, confirm green.**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(canvas): delete key fires IGraphInteractionHandler for mixed selection"
```

---

## Phase 8 — Sample app migration

### Task 21: Sample `GraphInteractionHandler` + delete wiring

**Files:**
- Create/modify: `samples/NodiumGraph.Sample/Handlers/SampleGraphInteractionHandler.cs`
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml.cs` (wire the new handler)
- Same for `samples/GettingStarted/`

**Step 1: Implement the handler**

```csharp
public class SampleGraphInteractionHandler : IGraphInteractionHandler
{
    private readonly Graph _graph;
    public SampleGraphInteractionHandler(Graph graph) => _graph = graph;

    public void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)
    {
        foreach (var conn in elements.OfType<Connection>().ToList())
            _graph.RemoveConnection(conn);
        foreach (var node in elements.OfType<Node>().ToList())
            _graph.RemoveNode(node);
    }
}
```

Order matters: remove connections first so node removal doesn't double-cascade (the library's internal cascade still handles whatever's left).

**Step 2: Wire** — assign in the sample's canvas setup code next to existing handler assignments.

**Step 3: Build + run the sample.** Select a mix of nodes and connections, press Delete, verify everything goes away in one operation.

**Step 4: Commit**

```bash
git add samples/
git commit -m "feat(sample): wire SampleGraphInteractionHandler for unified delete"
```

---

### Task 22: Sample connections with styled endpoints

**Files:**
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml.cs` (or wherever `DefaultConnectionStyle` is assigned)
- Add a second style used on a subset of demo connections

**Step 1: Change the default style to use `new ArrowEndpoint()` at target.**

**Step 2: Add a second style** (e.g. `InheritanceStyle`) with `DiamondEndpoint` at target. Apply to 1-2 demo connections to show per-connection variation.

**Step 3: Run the sample**, visually confirm: arrow on most connections, diamond on two; selection halo appears on click; delete removes them.

**Step 4: Commit**

```bash
git add samples/
git commit -m "feat(sample): demonstrate arrow and diamond endpoint decorations"
```

---

## Phase 9 — Documentation

### Task 23: Update `rendering-pipeline.md`

**Files:**
- Modify: `docs/userguide/3-reference/rendering-pipeline.md`

Describe the updated connection pass: world-space geometry, inset stroke, halo-then-stroke-then-endpoint draw order, bucketing by `IsFilled`.

Commit: `docs(userguide): document endpoint decoration render pipeline`

---

### Task 24: New/updated connection API reference

**Files:**
- Create or modify: `docs/userguide/3-reference/connection-api.md`

Content: `IEndpointRenderer` contract, each built-in with its geometry diagram, guidance for authoring a custom renderer, notes on `GetInset` semantics and `IsFilled` bucketing.

Commit: `docs(userguide): add IEndpointRenderer reference`

---

### Task 25: How-to — style connections with arrowheads

**Files:**
- Create: `docs/userguide/2-how-to/style-connections-with-arrowheads.md`

Worked example: two `ConnectionStyle` instances, arrow vs diamond, assigned via an `IConnectionStyleProvider` (or however the sample resolves style — confirm current mechanism).

Commit: `docs(userguide): add how-to for styling connection endpoints`

---

### Task 26: How-to — select and delete connections

**Files:**
- Create: `docs/userguide/2-how-to/select-and-delete-connections.md`

Worked example: implementing `ISelectionHandler` with mixed `IGraphElement` input, wiring `IGraphInteractionHandler` for delete, showing how to build an undo group around a single `OnDeleteRequested` call.

Commit: `docs(userguide): add how-to for connection selection and delete`

---

## Final verification

After Task 26:

1. `dotnet build` — clean
2. `dotnet test` — all tests pass (target: previous 395 + ~40 new = ~435)
3. Run the sample, smoke-test visually:
   - Default connections render with arrowheads
   - A subset renders with diamonds
   - Clicking a connection selects it (halo visible)
   - Ctrl-click toggles; marquee picks mixed types
   - Delete key removes mixed selections in one shot
4. `git log --oneline` — confirm one commit per task, ~26 commits
5. Open a PR against `main` with the design doc linked in the description

## Out of scope for this plan (explicit deferrals, do not tackle)

- StepRouter port-direction awareness
- Back-node port hit-test z-order
- Connection hover highlight
- Endpoint geometry caching by (renderer, thickness bucket)
- Mid-line connection decorations (labels, junction dots)
- Bitmap/gradient endpoint content
