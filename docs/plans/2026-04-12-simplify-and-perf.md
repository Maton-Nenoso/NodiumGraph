# Simplify & Perf Pass — 2026-04-12

## Context

Follow-up to the recent review-fix branch (`fix/remaining-review-fixes`, merged). A three-agent simplification scan surfaced remaining duplication and hot-path allocations. Findings below were verified against the source before planning; survey-level false positives from the agents have been dropped.

**Scope:** `src/NodiumGraph/` only. No behavior changes, no new features. Every task is a pure refactor or perf fix with the same external contract.

**Dropped from the original survey:**
- *Router endpoint extraction.* Only 2 lines of boilerplate in each of `BezierRouter`/`StepRouter`; `StraightRouter` already inlines (`StraightRouter.cs:14`). Not worth a helper.
- *Event-subscription symmetry audit on `NodiumGraphCanvas`.* Already verified clean in commit `5e16827`.
- *`NodiumGraphCanvas` split into smaller classes.* Real, but out of scope for a simplification pass — defer until a feature forces it.

---

## Verified findings

### P0 — Hot-path allocations in `CanvasOverlay.Render`

Path: `src/NodiumGraph/Controls/CanvasOverlay.cs`

1. **Per-frame `StreamGeometry` for Diamond/Triangle ports** — `CanvasOverlay.cs:179` and `:194`. A fresh `StreamGeometry` is opened and filled for every Diamond/Triangle port on every render. At 500 nodes × 20 ports this is thousands of allocations per frame.
2. **Per-frame `new Pen` for custom-styled ports** — `CanvasOverlay.cs:157-159`. When a `PortStyle` sets `Stroke` or `StrokeWidth`, the pen is allocated inline, bypassing the `GetOrCreatePen` cache used for the default case.
3. **FormattedText cache thrash on zoom** — two compounding problems:
   - `CanvasOverlay.cs:70-74` clears `_labelCache` wholesale whenever `|zoom - _lastZoom| > 0.0001`, i.e. on *every* scroll tick. The cache is effectively useless during the exact workload it's meant to optimize.
   - `CanvasOverlay.cs:233` keys the cache on raw `portLabelFontSize * zoom`, so even without the wholesale clear, incremental zoom would miss on every tick.
   - Additionally, `CanvasOverlay.cs:46` defines `InvalidateLabelCache()` with zero callers — dead code.
4. **Node-Style block also allocates per frame** — `CanvasOverlay.cs:105, 113`. `new Pen(...)` for selected / hovered border when a `NodeStyle` overrides the default brush or thickness. Same shape as finding 2.

### P0 — No viewport culling for connections

Path: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:1034-1043`

`ConnectionRenderer.Render` is called for every connection in the graph with no AABB test against the viewport. At 1000+ connections most are off-screen during zoom-in / pan, yet each still runs `router.Route(...)` and allocates a geometry. This is the single biggest throughput issue at target scale.

Additional: `connectionPen` on line 1038 is rebuilt every render from `DefaultConnectionStyle`. It only changes when the style changes, so it can be cached like the overlay pens.

### Dropped — Wasted `ToList()` on connection cut

Originally proposed removing `Graph.Connections.ToList()` at `NodiumGraphCanvas.cs:769`. **Not safe.** The shipped sample handler calls `graph.RemoveConnection(connection)` synchronously from `OnConnectionDeleteRequested` (`samples/NodiumGraph.Sample/MainWindow.axaml.cs:161`). Dropping the defensive copy would throw `InvalidOperationException` during cut-delete in the very first consumer path we ship.

The copy is the correct shape for the current handler contract. Tightening the contract (e.g. "delete requests must be deferred") is a public-API change and out of scope for this pass. Leave the `ToList()` alone.

### P1 — Duplicated squared-distance hit test

Three sites, identical `dx*dx + dy*dy` shape:
- `FixedPortProvider.cs:71-73` (port hit radius)
- `DynamicPortProvider.cs:56-59` (reuse-threshold check)
- `DynamicPortProvider.cs:157-159` (max-distance clamp)

Small but the helper pays for itself in readability and gives us a single place to swap in SIMD later if it ever matters.

### P2 — Port-shape switch scattered between provider and overlay

`PortShape` is dispatched in at least:
- `CanvasOverlay.cs:161` (render path, 4 cases + new shapes require re-touching)
- Port hit-testing in providers uses `INodeShape` already, but `PortShape` itself has no polymorphic dispatch

This is the only "new abstraction" item in the plan. Gated on actually fixing finding P0.1 — the geometry cache is the reason to introduce it. Without the cache it's speculative; with the cache it gives us one file per port shape (geometry builder + cache entry) instead of a growing switch.

---

## Plan

Each task is a separate commit. Run `dotnet build` and `dotnet test` between tasks.

### Task 1 — `GeometryHelpers.DistanceSquared`

**Files:** new `src/NodiumGraph/Model/GeometryHelpers.cs` (or fold into an existing `Model` helper if one already exists — check first), `FixedPortProvider.cs`, `DynamicPortProvider.cs`.

**Change:**
```csharp
internal static class GeometryHelpers
{
    public static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
```

Replace the three duplicated sites. Keep the helper `internal` — this is library-internal plumbing, not part of the public surface.

**Done when:** `grep -n "dx \* dx + dy \* dy" src/NodiumGraph` returns zero hits (other than the helper itself).

### Task 2 — **Dropped.** See finding above.

### Task 3 — Cache `connectionPen` in `NodiumGraphCanvas`

**File:** `NodiumGraphCanvas.cs:1037-1038`.

**Critical constraint — must preserve in-place mutation behavior.** `IConnectionStyle` (`IConnectionStyle.cs:8-13`) is a plain getter interface. The built-in `ConnectionStyle` class is immutable, but consumers can supply mutable implementations. The current per-render `new Pen(style.Stroke, style.Thickness, style.DashPattern)` picks up in-place mutation of any of those three properties transparently. A cache keyed only on the style instance reference would silently break that contract.

**Change:** Match the existing sentinel-field pattern in `CanvasOverlay.GetOrCreatePen` (`CanvasOverlay.cs:48-58`). Add four fields to `NodiumGraphCanvas`:
```csharp
private Pen? _cachedConnectionPen;
private IBrush? _lastConnectionStroke;
private double _lastConnectionThickness;
private IDashStyle? _lastConnectionDashPattern;
```

In `Render`, replace lines 1037-1038 with a sentinel probe:
```csharp
var style = DefaultConnectionStyle;
if (_cachedConnectionPen is null
    || !ReferenceEquals(_lastConnectionStroke, style.Stroke)
    || _lastConnectionThickness != style.Thickness
    || !ReferenceEquals(_lastConnectionDashPattern, style.DashPattern))
{
    _cachedConnectionPen = new Pen(style.Stroke, style.Thickness, style.DashPattern);
    _lastConnectionStroke = style.Stroke;
    _lastConnectionThickness = style.Thickness;
    _lastConnectionDashPattern = style.DashPattern;
}
var connectionPen = _cachedConnectionPen;
```

**Why sentinel fields, not property-change hooks or identity keys:**
- Sentinel comparison on the three getter values naturally picks up both "consumer replaces the `DefaultConnectionStyle` instance" *and* "consumer mutates the existing instance in place". Both cases are observable via value-sentinel comparison; neither requires a subscription.
- This preserves the exact observable behavior of the pre-cache code for all current and future `IConnectionStyle` implementations.
- It also avoids touching `NodiumGraphCanvas.OnPropertyChanged` at `:1168` at all — no invalidation plumbing needed.
- `ReferenceEquals` for brush and dash-pattern comparison is justified because those types do not provide value-equality guarantees. Thickness is a `double` with direct `!=`.

**Edge case — same values, different brush instance:** If a consumer swaps `Stroke` to a freshly-allocated `SolidColorBrush` with identical color, the cache rebuilds the pen. One extra allocation per swap. Acceptable — this is strictly better than the status quo (which rebuilds every frame regardless).

**Done when:** `Render` allocates zero `Pen` objects on repeat frames when `DefaultConnectionStyle`'s three properties are unchanged; mutating *any* of the three (in place or by replacing the instance) rebuilds the pen on the next render.

### Task 4 — Viewport culling for connection render (no public API change)

**File:** `NodiumGraphCanvas.cs:1034-1043`. **Do not touch `IConnectionRouter`** — it is public (`IConnectionRouter.cs:9`) and a consumer-implemented strategy interface. Keep all culling logic internal to `NodiumGraphCanvas`.

**Change:**
1. Before the loop, compute the viewport AABB in world coordinates once:
   ```csharp
   var viewportWorld = new Rect(
       transform.ScreenToWorld(default),
       transform.ScreenToWorld(new Point(Bounds.Width, Bounds.Height)));
   // Normalize in case zoom flips coordinates (shouldn't, but cheap).
   viewportWorld = new Rect(viewportWorld.TopLeft, viewportWorld.BottomRight);
   ```
   If `ViewportTransform` does not already expose a `ScreenToWorld(Point)` method, use whatever equivalent exists (the class is already used at lines 1057-1059 for the cut-intersection test, so at least one inverse transform exists).
2. Inflate by `connectionPen.Thickness / zoom` for stroke bleed near the edge.
3. Per connection, compute a loose world-space AABB from the two port positions: `Rect.FromPoints(sourceAbs, targetAbs)` (or build it inline with min/max — whichever Avalonia 12 actually provides; verify via `mcp__avalonia-docs`).
4. If the router is `RouteKind.Bezier`, inflate the per-connection rect to bound the bezier control-point excursion. The current `BezierRouter` pushes control points horizontally by `Math.Max(|dx| * 0.4, 30)`. Two options:
   - **Exact**: `var offset = Math.Max(Math.Abs(dx) * 0.4, BezierControlOffsetMin); rect = rect.Inflate(new Thickness(offset, 0, offset, 0));` where `BezierControlOffsetMin = 30` is a private constant in `NodiumGraphCanvas` that mirrors `BezierRouter.MinOffset`. Document the coupling in a one-line comment referencing the router.
   - **Conservative**: `rect.Inflate(Math.Abs(dx) * 0.4 + 30)` — over-inflates when `|dx| * 0.4 < 30` (short connections) but is one expression instead of two. Simpler; strictly safe for culling; rejects slightly fewer connections.
   Pick the exact form. The conservative form was in an earlier draft and was incorrectly described as "exact" — it isn't. Add a small vertical slack (`+ connectionPen.Thickness`) regardless.
5. Skip `ConnectionRenderer.Render` if `!viewportWorld.Intersects(connectionRect)`.

**Why this is allowed to know `RouteKind.Bezier`:** the same file already branches on `RouteKind.Bezier` at line 1055 for cut-line intersection. The coupling already exists; we're not making it worse. If a new router kind lands later, its culling rect defaults to the endpoint AABB (no cull) — conservative, correct, slow only for that specific kind until it's taught its own bounds.

**Extract** the per-connection rect computation into a private static helper on `NodiumGraphCanvas` so it can be unit-tested and so the same logic can be reused later by the cut-intersection path (which currently recomputes `router.Route` for every connection — a candidate for a future pass, not this one).

**Done when:** debugger breakpoint on `ConnectionRenderer.Render` is not hit for connections entirely outside the viewport on a 1000-connection test graph, and on-screen rendering is visually identical to before.

### Task 5 — Cache port geometries (Diamond/Triangle)

**File:** `CanvasOverlay.cs`.

**Critical note — do not bake `screenPos` into cached geometry.** The current inline code at `CanvasOverlay.cs:182-186` builds `StreamGeometry` points in absolute screen coordinates (e.g. `new Point(screenPos.X, screenPos.Y - scaledRadius)`). If we cached that geometry directly, reusing it for a port at a different screen position would draw the diamond in the wrong place. The cache must store **origin-centered** geometry and translate at draw time.

**Change:**
1. Add a field `private readonly Dictionary<(PortShape, double bucketedRadius), Geometry> _portGeometryCache = new();`.
2. Bucket `scaledRadius` to nearest 0.5: `var bucketedRadius = Math.Round(scaledRadius * 2) / 2;`. Sub-0.5 differences are invisible at display resolution.
3. Build the cached geometry with points relative to `(0, 0)`:
   ```csharp
   // Diamond, origin-centered:
   ctx.BeginFigure(new Point(0, -bucketedRadius), true);
   ctx.LineTo(new Point(bucketedRadius, 0));
   ctx.LineTo(new Point(0, bucketedRadius));
   ctx.LineTo(new Point(-bucketedRadius, 0));
   ctx.EndFigure(true);
   ```
4. At draw time, translate into place with a matrix push:
   ```csharp
   using (context.PushTransform(Matrix.CreateTranslation(screenPos.X, screenPos.Y)))
   {
       context.DrawGeometry(fill, pen, cachedGeo);
   }
   ```
   Verify the exact `PushTransform` API with `mcp__avalonia-docs` — Avalonia 12 has shifted this several times and `using` disposal may or may not be the correct shape.
5. **Explicit invalidation.** There is no existing hook to reuse — the only cache infrastructure in `CanvasOverlay` today is the inline zoom-change clear for `_labelCache` at `CanvasOverlay.cs:70-74`, plus a dead `InvalidateLabelCache()` method at `:46` (no callers; delete it in this task). For the geometry cache:
   - Do **not** clear on every zoom tick. The whole point of bucketing is to reuse entries across small zoom changes.
   - Bound the cache at 64 entries. On overflow, clear the whole dictionary (simpler than LRU, and the refill cost is one geometry per unique `(shape, bucketedRadius)` seen — bounded by the number of distinct port sizes in the graph).
   - Expose `internal void InvalidatePortGeometryCache() => _portGeometryCache.Clear();` so future theme-change plumbing can hook it if needed. Keep it unused for now — do not invent callers.

**Done when:** profiler shows no `StreamGeometry..ctor` in `Render` hot samples on repeat frames, and the sample app renders Diamond/Triangle ports at visibly the same positions as before.

### Task 6 — Extend pen cache to styled ports and node borders

**File:** `CanvasOverlay.cs`.

**Change:**
1. Add a keyed pen cache alongside the existing `GetOrCreatePen` (which is a single-slot cache, not a dictionary). New field:
   ```csharp
   private readonly Dictionary<(IBrush brush, double thickness), Pen> _styledPenCache
       = new(new BrushThicknessComparer());
   ```
   Provide the custom comparer explicitly — do **not** rely on the default. A plain `Dictionary<(IBrush, double), Pen>` uses `ValueTuple`'s equality, which calls `EqualityComparer<IBrush>.Default.Equals`. For most Avalonia brush types this currently falls through to `Object.Equals` (i.e. reference equality), but that is an implementation accident, not a guarantee — if a future Avalonia release gives any `Brush` subclass value-based equality, the cache semantics change silently. An explicit comparer makes the identity contract visible:
   ```csharp
   private sealed class BrushThicknessComparer
       : IEqualityComparer<(IBrush brush, double thickness)>
   {
       public bool Equals((IBrush brush, double thickness) x, (IBrush brush, double thickness) y)
           => ReferenceEquals(x.brush, y.brush) && x.thickness == y.thickness;
       public int GetHashCode((IBrush brush, double thickness) obj)
           => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.brush), obj.thickness);
   }
   ```
2. The cache is identity-keyed on brush: two distinct style objects with identical brush values will each get their own pen. Acceptable — the typical graph has < 10 distinct style instances.
3. Use the new cache at:
   - `CanvasOverlay.cs:157-159` (port pens)
   - `CanvasOverlay.cs:105` (selected-border pen, `NodeStyle` override)
   - `CanvasOverlay.cs:113` (hovered-border pen, `NodeStyle` override)
4. Leave any dash-patterned pen path alone — `IDashStyle` equality is tricky and dashes are rare here.

**Cache invalidation:** There is **no existing theme/style-change hook in `CanvasOverlay`** (verified — only `_labelCache.Clear()` on zoom at line 72, plus dead `InvalidateLabelCache()` at line 46). Do not invent one in this task. Instead:
- Bound the dictionary at 32 entries. On overflow, clear and rebuild next frame.
- In practice, the entry count is bounded by the number of distinct `(IBrush, double)` instances used across the graph's port and node styles, typically < 10. The 32-entry cap is for pathological cases only.
- If a consumer mutates a brush instance in place (rather than replacing it), the cached pen will point at stale state. This is the same constraint Avalonia already imposes on directly-held brushes — document in a comment on the cache field.

**Done when:** `new Pen(` in the render hot path appears only inside the cache helpers (`GetOrCreatePen` and the new styled-cache lookup).

### Task 7 — Bucket FormattedText cache key and drop the zoom-tick clear

**File:** `CanvasOverlay.cs`.

**Current shape (re-verified):**
- Line 70-74 clears `_labelCache` whenever `|zoom - _lastZoom| > 0.0001`. This fires on *every* scroll-wheel tick, meaning the cache is effectively useless during zoom — the very workload it is supposed to help.
- Line 233 builds the cache key from raw `portLabelFontSize * zoom`, so even without the line-72 clear, incremental zoom would thrash the cache.
- Line 46 defines `InvalidateLabelCache()`, which has zero callers across the repo. Dead code.

**Change:**
1. **Delete** the zoom-tick clear at `CanvasOverlay.cs:70-74` along with the `_lastZoom` field if it becomes unused.
2. **Bucket** the font size in the cache key: `var bucketedSize = Math.Round(portLabelFontSize * zoom * 2) / 2;`. Use `bucketedSize` both in the key and in the `FormattedText` constructor at `:241` — otherwise the rasterized text won't match the key and rounding will flicker.
3. **Bound** `_labelCache` at 256 entries. On overflow, clear and rebuild. Justification for 256: unique-label count × distinct bucketed sizes × distinct brushes. A realistic upper bound in a 500-node graph is well under 256.
4. **Delete** the unused `InvalidateLabelCache()` method at `:46`. If a future theme hook needs it, add it then. Do not keep dead code as speculative scaffolding.

**Risk:** Rendering the label at a bucketed size instead of the exact `portLabelFontSize * zoom` means labels are quantized to 0.5-px steps during zoom. Verify visually — at typical zoom ranges this should be invisible. If it isn't, switch to 0.25-px buckets or a smarter rounding, but do not go back to raw keying.

**Done when:** continuous scroll-wheel zoom does not grow `_labelCache` on every tick (add a temporary assertion in debug builds), and labels render with the same visual result as before (eyeball check in the sample app).

### Task 8 — (Gated on Task 5) `IPortShapeRenderer` polymorphism

**Decision point:** Only do this if Task 5's cache code starts to feel like the switch is in the wrong place. If Task 5 lands cleanly, stop — don't introduce an interface for two shapes.

If we do it:
- New `internal interface IPortShapeRenderer { void Draw(DrawingContext ctx, Point screenPos, double scaledRadius, IBrush fill, IPen pen); }` (or return a `Geometry` from a factory).
- One implementation per `PortShape` value; a static map `PortShape -> IPortShapeRenderer`.
- The switch in `CanvasOverlay` becomes a dictionary lookup.
- Adding a new port shape is then one file instead of a touching four call sites.

**Only justified if:** a fifth port shape lands, or port rendering spawns more state than the cache.

---

## Ordering and risk

Do in order: **1 → 3 → 7 → 6 → 5 → 4 → 8**. (Task 2 dropped.)

Rationale:
- Task 1 (`DistanceSquared` helper) is trivial and zero-risk — ship first to clear clutter.
- Task 3 (connection pen cache) is self-contained and the test surface is already there.
- Task 7 (label cache bucketing) is the smallest `CanvasOverlay` change and fixes an existing thrash bug.
- Task 6 (pen cache extension) touches three call sites in the same file; independently verifiable.
- Task 5 (port geometry cache) is the most involved `CanvasOverlay` change — the origin-centered-geometry-plus-translate pattern needs visual verification.
- Task 4 (viewport culling) touches `NodiumGraphCanvas` render and has the biggest throughput win. It's no longer the "highest risk" item now that `IConnectionRouter` is off-limits — the revised Task 4 is purely internal. Still do it last so any regressions are isolated from the `CanvasOverlay` work.
- Task 8 is optional and gated on Task 5's shape.

## Verification after each task

```
dotnet build
dotnet test
```

Optional, for P0 items: run the sample app, pan/zoom a 500-node graph, eyeball that nothing jumps or flickers (rendering regressions from culling or cache bugs usually show up visually before they show up in tests).

## Out of scope (on purpose)

- Splitting `NodiumGraphCanvas`. Too big for a simplification pass; defer until a feature needs it.
- Replacing `PortShape` enum with `IPortShape` beyond what Task 8 proposes. The public surface is currently a plain enum and breaking it is not justified by current pain.
- Spatial indexing for node hit-tests. Mentioned by the efficiency agent but `HitTestNode` is cheap enough at current scale; revisit only if a profiler flags it on real graphs.
