---
title: Connection endpoint decorations & unified selection
tags: [spec, architecture, plan]
status: active
created: 2026-04-15
updated: 2026-04-15
---

# Connection endpoint decorations & unified selection

## Motivation

Today every connection renders as a plain stroked path with no endpoint decoration, and connections are not interactive — only nodes can be clicked, selected, or targeted by the delete key. Two consumer-facing needs surfaced together:

1. **Endpoint shapes.** Real graph editors need arrowheads (directed flows), diamonds (UML composition/aggregation), bars (cardinality), circles, and "none" per endpoint. Both ends need independent control so a single connection can render, for example, a filled arrow at the target and nothing at the source.
2. **Connection selection.** Consumers want to attach domain data to connections just like they do to nodes — subclassing `Connection` already enables that, but without a way to *select* a connection there is no way to route UI affordances (inspectors, property panels, delete) to it.

These two features share a geometry layer (hit-testing reuses the rendered path + endpoint geometries), so they land together.

## Scope

**In scope:**

- `IEndpointRenderer` strategy interface + five built-in implementations (`NoneEndpoint`, `ArrowEndpoint`, `DiamondEndpoint`, `CircleEndpoint`, `BarEndpoint`).
- Per-end independent endpoint decoration on `IConnectionStyle` (`SourceEndpoint`, `TargetEndpoint`).
- Line inset at decorated endpoints so the stroke terminates at the base of the shape, not inside it.
- Unified `IGraphElement` marker interface implemented by `Node` and `Connection`.
- `Graph.SelectedItems` as the canonical selection set; `SelectedNodes`/`SelectedConnections` become filtered mirror views that keep existing bindings working.
- Click + ctrl-click + marquee selection of connections, mixed with nodes.
- `IGraphInteractionHandler.OnDeleteRequested(IReadOnlyCollection<IGraphElement>)` as the unified delete entry point.
- Selection halo rendered as an under-stroke pass in the same `ConnectionRenderer.Render` call.
- World-space connection geometry caching (prerequisite for hit-testing, incidental rendering cleanup).
- Hit-testing via `geometry.StrokeContains || geometry.FillContains` with screen-space-constant pixel tolerance.

**Out of scope:**

- Mid-line connection decorations (labels, junction dots, weight badges).
- Connection hover highlighting.
- Selection-aware routing (spreading parallel selected connections, etc.).
- StepRouter port-direction awareness (still deferred from prior work).
- Back-node port hit-test z-order fix (still deferred).
- Bitmap or gradient endpoint content — `IEndpointRenderer` is geometry-only by design.

## Architecture

### New types

| Type | Kind | Purpose |
|---|---|---|
| `IGraphElement` | marker interface | Empty. Implemented by `Node` and `Connection`. Serves as the type union for unified selection and delete. |
| `IEndpointRenderer` | strategy interface | `Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)`, `double GetInset(double strokeThickness)`, `bool IsFilled { get; }`. |
| `NoneEndpoint` | sealed class, singleton | Sentinel. `BuildGeometry` returns empty geometry, `GetInset` returns 0. |
| `ArrowEndpoint(size, filled)` | sealed class | Triangle. Default size 8, filled true. |
| `DiamondEndpoint(size, filled)` | sealed class | Rhombus. For UML aggregation/composition. |
| `CircleEndpoint(radius, filled)` | sealed class | Disc. |
| `BarEndpoint(width)` | sealed class | Perpendicular bar. Never filled. |
| `RouteTangents` | readonly record struct | `From(IReadOnlyList<Point> points, RouteKind kind) → (Vector source, Vector target)`. Handles 4-point bezier and N-point polyline. |
| `ConnectionHitTester` | internal static | Iterates connections in reverse z-order and returns the topmost whose cached hit-test geometry contains the world point within tolerance. |
| `IGraphInteractionHandler` | handler interface | `OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements)`. New top-level handler slot on the canvas. |
| `CachedConnectionGeometry` | internal readonly record struct | `(Geometry Stroke, Geometry HitTestShape, Rect Bounds, int RouteVersion)`. Lives in a per-canvas `Dictionary<Guid, CachedConnectionGeometry>`. |

### Modified types

| Type | Change |
|---|---|
| `Node` | Implements `IGraphElement`. Marker only, no behavior change. |
| `Connection` | Implements `IGraphElement`. Marker only, no behavior change. |
| `IConnectionStyle` | Adds `IEndpointRenderer? SourceEndpoint`, `IEndpointRenderer? TargetEndpoint`. Null means no decoration. |
| `ConnectionStyle` | Constructor gains two optional endpoint args defaulting to `null`. |
| `ConnectionRenderer.CreateGeometry` | Builds geometry in **world space** (not screen space). Produces a `GeometryGroup { stroke, sourceDecoration?, targetDecoration? }`. Applies endpoint insets to the stroke start/end. |
| `ConnectionRenderer.Render` | Wraps the draw in `context.PushTransform(viewport)`. Renders selection halo (if selected) as a first `DrawGeometry` pass with a wider, semi-transparent pen, then the normal stroke, then the endpoint fills/strokes in up to two more passes bucketed by `IsFilled`. |
| `Graph` | Adds `ObservableCollection<IGraphElement> SelectedItems`. `SelectedNodes` becomes a `ReadOnlyObservableCollection<Node>` mirror-view of `SelectedItems.OfType<Node>()`. New symmetric `SelectedConnections`. `RemoveNode` and `RemoveConnection` remove from `SelectedItems`. |
| `ISelectionHandler.OnSelectionChanged` | Param type changes from `IReadOnlyCollection<Node>` to `IReadOnlyCollection<IGraphElement>`. |
| `INodeInteractionHandler` | `OnDeleteRequested` is **removed**. Other members (`OnNodesMoved`, `OnNodeDoubleClicked`) unchanged. |
| `IConnectionHandler.OnConnectionDeleteRequested` | Unchanged. Different semantics (single-connection imperative, e.g. context menu) from the new unified delete (keyboard batch). |
| `NodiumGraphCanvas` | New `IGraphInteractionHandler? GraphInteractionHandler` styled property. New `_connectionGeometryCache` dictionary alongside existing render caches. Pointer-press pipeline gains a connection hit-test step after node/port rejection. Marquee extended to include connections by route-rect intersection. New theme resource key `ConnectionSelectionHaloBrushKey`. |

### Render order

1. Grid
2. Connections — each connection draws in one `Render` call containing up to three `DrawGeometry` passes:
   1. Selection halo (if selected): wider semi-transparent pen, covers stroke + both endpoint geometries.
   2. Stroke: normal pen, stroke geometry only.
   3. Endpoints: filled endpoints in one pass, stroked-only endpoints in another.
3. Drag preview (canvas overlay)
4. Node containers (each with its own `NodeAdornmentLayer` — unchanged from prior work)
5. Marquee + port validation feedback (canvas overlay)

## Endpoint decoration system

### Interface shape

```csharp
public interface IEndpointRenderer
{
    /// World-space length to shorten the connection line by at this endpoint,
    /// so the stroke ends cleanly at the base of the shape instead of
    /// running through it. Must return 0 for NoneEndpoint.
    double GetInset(double strokeThickness);

    /// True if BuildGeometry returns a closed shape intended to be filled
    /// with the connection's stroke brush. False for open shapes that are
    /// stroke-only (e.g. BarEndpoint, open arrowheads).
    bool IsFilled { get; }

    /// Build the decoration geometry in world coordinates.
    /// `tip` is the port center. `direction` is a unit vector pointing
    /// outward along the curve tangent at this endpoint.
    Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness);
}
```

Two-method interface + one property. Implementations are effectively value objects: stateless, immutable, cheap to construct. Consumers are encouraged to construct once and share (`static readonly ArrowEndpoint Default = new()`).

### Rendering pipeline

`ConnectionRenderer.CreateGeometry`:

1. Call `router.Route(source, target)` → world-space points list.
2. Compute `(sourceTangent, targetTangent) = RouteTangents.From(points, router.RouteKind)`.
3. If `style.SourceEndpoint` is non-null, inset `points[0]` outward by `source.GetInset(thickness)` along `-sourceTangent`. Mirror at the target end.
4. Build the stroke as a `StreamGeometry` through the possibly-inset points (bezier if 4 points + `RouteKind.Bezier`, else polyline).
5. If endpoint renderers are non-null, call `BuildGeometry` on each and collect into a `GeometryGroup` child list.
6. Return a `GeometryGroup { stroke, sourceGeo?, targetGeo? }` in world space.

`ConnectionRenderer.Render`:

1. Push viewport transform onto the drawing context.
2. If connection is selected, draw halo: `context.DrawGeometry(null, haloPen, hitTestShape)` where `haloPen` is a wider (`thickness + halo extra`) semi-transparent pen from theme resources.
3. Draw stroke: `context.DrawGeometry(null, strokePen, strokeGeometry)`.
4. Draw filled endpoints: `context.DrawGeometry(strokeBrush, strokePen, filledEndpointGroup)` — only emitted if at least one endpoint `IsFilled`.
5. Draw stroked-only endpoints: `context.DrawGeometry(null, strokePen, openEndpointGroup)` — only emitted if at least one endpoint is non-filled.
6. Pop transform.

The common case (no endpoints, not selected) collapses to a single `DrawGeometry` call identical in behavior to today's output — the regression budget is zero for existing styles.

### Built-in renderers

| Renderer | Geometry at direction (1,0), tip at origin | Inset |
|---|---|---|
| `NoneEndpoint` | empty | 0 |
| `ArrowEndpoint(size, filled)` | triangle with vertices at `(0,0)`, `(-size, size/2)`, `(-size, -size/2)`, closed | `size` when filled, `size * 0.9` when open |
| `DiamondEndpoint(size, filled)` | rhombus with vertices at `(0,0)`, `(-size/2, size/2)`, `(-size, 0)`, `(-size/2, -size/2)`, closed | `size` |
| `CircleEndpoint(radius, filled)` | circle centered at `(-radius, 0)` with radius `radius`, closed | `radius * 2` |
| `BarEndpoint(width)` | line from `(-width/2, -size/2)` to `(-width/2, size/2)`, open | `width / 2` |

Each renderer builds its canonical geometry (at the origin, pointing `(1,0)`) and applies a `MatrixTransform` composed of rotation (from `direction`) + translation (to `tip`). Rotation matrix is `[[dx, -dy], [dy, dx]]` from the unit direction vector — no trig calls.

### Caching

First pass: **no endpoint geometry caching**. Build per render. The cost per connection is tiny (single-digit `PathFigure` vertices), and we only render visible connections thanks to viewport cull. If profiling shows endpoint construction in the hot path, a follow-up PR can cache keyed by `(renderer, thickness-bucket)` alongside the existing `_portGeometryCache` on the canvas.

Stroke geometry **is** cached per-connection in the canvas `_connectionGeometryCache` — this cache is load-bearing for hit-testing and incidentally also serves rendering.

## Selection system

### Graph model changes

```csharp
public class Graph
{
    public ObservableCollection<IGraphElement> SelectedItems { get; }
    public ReadOnlyObservableCollection<Node> SelectedNodes { get; }
    public ReadOnlyObservableCollection<Connection> SelectedConnections { get; }
    // ... existing API unchanged ...
}
```

`SelectedNodes` and `SelectedConnections` are backed by private `ObservableCollection<T>` instances that subscribe to `SelectedItems.CollectionChanged` and mirror the relevant subset. They are real collections (not LINQ views), so consumer code that casts to `INotifyCollectionChanged` — the Avalonia 12 trick from `CLAUDE.md` — continues to work.

`RemoveNode` removes the node from `SelectedItems` before its connections are cascaded. `RemoveConnection` removes the connection from `SelectedItems`. These are the only library-owned write sites to the underlying selection.

### Handler surface

```csharp
public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected);
}

public interface IGraphInteractionHandler
{
    void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements);
}
```

`INodeInteractionHandler.OnDeleteRequested` is removed entirely. `IConnectionHandler.OnConnectionDeleteRequested` is kept — it represents a different semantic (single-connection imperative, e.g. context menu X button) from the keyboard-triggered batch delete.

### Interaction semantics

| Input | Result |
|---|---|
| Click on node | `SelectedItems = { node }` |
| Click on connection | `SelectedItems = { connection }` |
| Click on empty canvas | `SelectedItems.Clear()` |
| Ctrl+click on any element | Toggle that element in `SelectedItems` |
| Marquee drag | Every node whose rect intersects + every connection whose route intersects becomes selected (batched as one `CollectionChanged` event) |
| Ctrl+marquee drag | Same as marquee but additive |
| Click on port | Starts connection draw; selection unchanged |
| Delete key | Fires `IGraphInteractionHandler.OnDeleteRequested(SelectedItems.ToList())` |

Connections only hit-test on mouse-down when the point misses all nodes and ports — node/port hit-test wins when they overlap, so a port sitting on a connection line still grabs the drag. Connection hit-test iterates in reverse z-order (last in list = topmost).

## Hit-testing

### World-space geometry switch

Today `ConnectionRenderer.CreateGeometry` bakes `transform.WorldToScreen` into every point, so the cached geometry invalidates on every pan/zoom. That's fine for rendering (we redraw every frame) but wrong for hit-testing (we'd rebuild inside a click handler). 

The change: `CreateGeometry` builds entirely in world space, and `Render` wraps the draw in `context.PushTransform(viewport)`. Rendering output stays pixel-identical. This also halves the geometry object churn on pan, since the renderer now reuses cached world-space geometries across frames.

### Cache

```csharp
private readonly Dictionary<Guid, CachedConnectionGeometry> _connectionGeometryCache = new();

internal readonly record struct CachedConnectionGeometry(
    Geometry Stroke,
    Geometry HitTestShape,
    Rect WorldBounds,
    int RouteVersion);
```

Invalidation triggers (scoped — not all-or-nothing):

- Connection added → add entry on next render
- Connection removed → remove entry
- Connection's source or target port moves (owner node moved) → invalidate that entry
- Router swapped → invalidate all
- `IConnectionStyle` endpoint changed for a connection → invalidate affected entry

No invalidation on pan or zoom.

### Hit-test algorithm

```csharp
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

- `worldTolerance = 8 / ViewportZoom` so the pixel feel stays constant at any zoom.
- `Bounds.Inflate(tolerance).Contains(point)` cheap-reject avoids expensive `StrokeContains` on far-away connections.
- `StrokeContains || FillContains` correctly handles both stroke hits on the line and interior hits on filled endpoint shapes.
- Iterates reverse so topmost wins.

At 1000 connections the reject is 1000 rect tests + a small number of `StrokeContains` calls — well inside the NFR budget.

### Marquee picking

Marquee re-uses the same cache: for each connection, a bounding-box pre-check against the marquee rect, then a finer segment/bezier-flatten intersection against the marquee rectangle. Anything that intersects goes into the new selection batch.

## Testing

### Test files and cases

| File | Coverage |
|---|---|
| `EndpointRendererTests.cs` (new) | For each built-in: geometry bounds at thickness 1/2/8; `GetInset` matches doc values; rotated geometry (direction = up/down/left/right/diagonals) places tip at the expected world point. Assert on specific geometry vertices, not pixel goldens. |
| `RouteTangentsTests.cs` (new) | 4-point bezier tangent from `cp2→p3` and `p0→cp1`; N-point polyline from last/first segment; zero-length route returns zero vector; single-point route returns zero vector (renderer must skip endpoint draw). |
| `ConnectionRendererTests.cs` (extend) | Geometry includes endpoint children when style has non-null endpoints; inset shortens the stroke; `NoneEndpoint` output equals today's output exactly (regression gate); world-space output (no viewport baked in). |
| `ConnectionHitTesterTests.cs` (new) | Click on line → hit; click beyond tolerance → miss; click on filled-arrow interior → hit via `FillContains`; click on open bar → hit via `StrokeContains`; overlapping connections → topmost wins; tolerance scales with zoom. |
| `GraphTests.cs` (extend) | `SelectedItems.Add(node)` fires a `SelectedNodes.CollectionChanged` event; same for connections; `RemoveNode` removes from `SelectedItems`; `RemoveConnection` ditto; cascade removal (node delete removes both the node and its connections from `SelectedItems`). |
| `NodiumGraphCanvasTests.cs` (extend) | Click on connection fires `ISelectionHandler` with `{ connection }`; ctrl+click toggles; marquee across mixed area picks both types in one batch; delete key fires `IGraphInteractionHandler.OnDeleteRequested` with the full `SelectedItems`. |
| `ConnectionRenderPipelineTests.cs` (new, optional structural pin) | Halo pass renders before stroke; halo uses `ConnectionSelectionHaloBrushKey` from theme resources; endpoint geometries invalidate on connection style change. |

### TDD order

1. `IEndpointRenderer` interface + `NoneEndpoint` + `ArrowEndpoint` (red/green/refactor).
2. `RouteTangents` helper.
3. `ConnectionRenderer` integration (world-space switch, endpoint rendering, halo pass).
4. Remaining endpoint shapes (`DiamondEndpoint`, `CircleEndpoint`, `BarEndpoint`) in parallel.
5. `IGraphElement` marker + `Graph` selection model changes.
6. `ConnectionHitTester` + cache plumbing on `NodiumGraphCanvas`.
7. Canvas interaction wiring (click, ctrl+click, marquee, delete).
8. `IGraphInteractionHandler` + sample app migration.
9. Documentation updates.

## Breaking changes (pre-1.0, all free)

1. `IConnectionStyle` gains `SourceEndpoint` and `TargetEndpoint`. Custom implementers must add them. Built-in `ConnectionStyle` accepts them as optional constructor args defaulting to null.
2. `ISelectionHandler.OnSelectionChanged` parameter type: `IReadOnlyCollection<Node>` → `IReadOnlyCollection<IGraphElement>`. Migration: consumer adds `.OfType<Node>()`.
3. `INodeInteractionHandler.OnDeleteRequested` is **removed**. Replaced by `IGraphInteractionHandler.OnDeleteRequested`, which is a new handler slot on the canvas (`NodiumGraphCanvas.GraphInteractionHandler`).
4. `Node` and `Connection` now implement marker interface `IGraphElement`. Additive.
5. `ConnectionRenderer.CreateGeometry` produces world-space geometry. Internal caller-only (`NodiumGraphCanvas.RenderConnections`); safe.
6. `Graph.SelectedNodes` changes from `ObservableCollection<Node>` to `ReadOnlyObservableCollection<Node>`. Consumer code that added/removed from `SelectedNodes` directly must migrate to `SelectedItems`.

## Documentation

- `docs/userguide/3-reference/rendering-pipeline.md` — connection pass now describes halo + endpoints + inset + world-space geometry.
- `docs/userguide/3-reference/connection-api.md` (new or extended) — `IEndpointRenderer` contract, built-ins, authoring guide for a custom renderer.
- `docs/userguide/2-how-to/style-connections-with-arrowheads.md` (new) — worked example using `ArrowEndpoint` and `DiamondEndpoint`.
- `docs/userguide/2-how-to/select-and-delete-connections.md` (new) — wiring `IGraphInteractionHandler` and responding to `ISelectionHandler.OnSelectionChanged` with mixed content.
- Sample app: new styled connections demonstrating arrows + diamonds; delete-key wiring via unified handler.

## Non-goals / explicit deferrals

- **Mid-line decorations** (labels, junction dots): its own design pass.
- **Connection hover highlight**: straightforward follow-up, not in this scope.
- **Endpoint geometry caching keyed on thickness bucket**: optimization, deferred until profiling demands it.
- **StepRouter direction awareness**: still deferred from the BezierRouter port-direction work.
- **Back-node port hit-test z-order**: still deferred from the per-node adornment layer work.
- **Bitmap/gradient endpoint content**: deliberately excluded — `IEndpointRenderer` is geometry-only.
