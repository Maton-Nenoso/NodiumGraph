# Full Codebase Code Review — 2026-04-11

**Scope:** All 48 library files under `src/NodiumGraph/` and 37 test files under `tests/NodiumGraph.Tests/`
**Commit:** 0403e70 (HEAD at time of review)
**Build:** Zero errors, 382 tests passing

---

## CRITICAL

### 1. Static `_fallbackTemplatesRegistered` — permanent template pollution

**File:** `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (line 61, 1287-1298)

The `_fallbackTemplatesRegistered` field is `static bool`, and `EnsureFallbackTemplates()` adds `DefaultTemplates.NodeTemplate` to `Application.Current.DataTemplates` permanently. Problems:

- Once registered, the fallback template stays in the application forever.
- If a consumer removes all canvas instances and creates new ones, the stale template remains.
- Multiple canvas instances share this static state.
- The template is never removed during `OnDetachedFromVisualTree`.

**Fix:** Track fallback template registration per-instance or provide cleanup in `OnDetachedFromVisualTree`. At minimum, remove the template from `Application.Current.DataTemplates` when the last canvas is detached.

---

### 2. Connection draw source port never gets commit/cancel resolve

**File:** `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (lines 582-591, 792-831)

In `OnPointerPressed`, the source port is resolved with `preview: true` (line 582). In `OnPointerReleased`, the target port is resolved with `preview: false` (line 808), but `CancelResolve` is only called on `_commitProvider` (the target's provider). The source port's provider never gets a commit resolve call.

When using `DynamicPortProvider` for the source node, dragging a connection from it will never create a source port (preview returns `null` for positions not near existing ports). New connections can't start from positions without existing ports.

**Fix:** Either commit the source provider when a connection completes, or document this as a known limitation. Consider adding a `CommitResolve` call for the source provider alongside the target commit.

---

### 3. `Graph.SelectedNodes` and `Node.IsSelected` can desynchronize

**Files:** `src/NodiumGraph/Model/Graph.cs` (lines 78-98), `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (lines 384-410)

`Graph.Select(node)` does NOT set `node.IsSelected`. `NodiumGraphCanvas.SelectNode` sets BOTH. This creates two problems:

- If a consumer calls `Graph.Select(node)` directly (public API), `node.IsSelected` remains `false` — the overlay won't draw the selection border.
- If a consumer sets `node.IsSelected = true` directly, `Graph.SelectedNodes` won't include the node.

**Fix:** Either have `Graph.Select/Deselect` also set `node.IsSelected`, or remove `Node.IsSelected` in favor of checking `Graph.SelectedNodes.Contains(node)`. The former is less breaking.

---

## IMPORTANT

### 4. Pen/Brush allocations every Render call

**Files:** `src/NodiumGraph/Controls/CanvasOverlay.cs` (lines 34-46, 88-113, 230-276), `src/NodiumGraph/Controls/GridRenderer.cs` (lines 79-80)

Every `Render()` call creates multiple `Pen` objects and calls `ResolveBrush`/`ResolvePen`. Key allocations per frame:

- `selectedBorderPen` and `hoveredBorderPen` (always allocated even if no nodes selected/hovered)
- Per-port custom Pen allocations (line 112) for every port with a custom style
- Preview/cutting pens in `_isDrawingConnection` block
- `FormattedText` objects for port labels (line 187) — particularly expensive
- `minorPen` and `majorPen` in GridRenderer (lines 79-80)

**Fix:** Cache pens and brushes at the class level, invalidating only when theme resources change. For port label `FormattedText`, consider caching by port label string.

---

### 5. `ConnectionRenderer.CreateGeometry` allocates per connection per frame

**File:** `src/NodiumGraph/Controls/ConnectionRenderer.cs` (lines 10-44)

For 1000 connections: 1000 `List<Point>` + 1000 `PathGeometry` + 1000 `PathFigure` objects per frame. With the stated target of 1000+ connections with smooth pan/zoom, this will cause noticeable GC pauses.

**Fix:** Use `StreamGeometry` (cheaper than `PathGeometry`) and pool or cache geometries that haven't changed since last frame.

---

### 6. `MinimapRenderer.Render` calls LINQ `.Min()`/`.Max()` 4x per frame

**File:** `src/NodiumGraph/Controls/MinimapRenderer.cs` (lines 44-47)

Each of `Min`/`Max` iterates all nodes. With minimap enabled, that's 4 full iterations of `graph.Nodes` every frame. `MinimapToWorld` (lines 119-122) repeats the same computation.

**Fix:** Compute bounds once per frame and pass through, or cache with dirty flag.

---

### 7. `HitTestNode` iterates Dictionary — no z-order guarantee

**File:** `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (lines 364-382)

Comment says "keep last match (topmost)" but nodes are stored in a `Dictionary<Node, ContentControl>`, which has no guaranteed iteration order corresponding to visual z-order. Overlapping node clicks hit an effectively random node.

**Fix:** Iterate `Graph.Nodes` (which has stable order from `ObservableCollection`) or maintain explicit z-ordering.

---

### 8. `CuttingLineIntersectsGeometry` allocates in hot path

**File:** `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (line 1058)

```csharp
var screenPoints = routePoints.Select(transform.WorldToScreen).ToList();
```

Called for every connection during cutting, allocating a new list each time.

**Fix:** Use a pre-allocated buffer or iterate without materializing to a list.

---

### 9. No `IDisposable` on `NodiumGraphCanvas`

The canvas subscribes to multiple external events (Graph, Node, Port, PortStyle PropertyChanged; IPortProvider events; ILayoutAwarePortProvider events). While `OnDetachedFromVisualTree` cleans up graph-level subscriptions, if the canvas is never attached to a visual tree (test scenarios, dynamic UI patterns), these subscriptions leak.

**Fix:** Implement `IDisposable` or ensure the `Graph` property setter handles full cleanup regardless of visual tree state.

---

### 10. `IPortProvider.ResolvePort` docs say "node-local coordinates" but code uses world-space

**File:** `src/NodiumGraph/Model/IPortProvider.cs` (line 20)

The doc says "Hit-test position in node-local coordinates" but the canvas passes world-space coordinates (NodiumGraphCanvas.cs line 358), and both `DynamicPortProvider.ResolvePort` and `FixedPortProvider.ResolvePort` compare against `AbsolutePosition` (world-space).

**Fix:** Correct the interface documentation to say "world-space coordinates".

---

### 11. `Graph.Select` uses `List.Contains` — O(n) per call

**File:** `src/NodiumGraph/Model/Graph.cs` (lines 78-98)

Selection uses `_selectedNodes.Contains(node)` which is O(n) on `List<Node>`. Batch select-all with 500+ nodes is O(n^2).

**Fix:** Use a `HashSet<Node>` backing set alongside the list for O(1) contains checks.

---

### 12. `Graph.RemoveNode` is O(n) per connection — no batch optimization

**File:** `src/NodiumGraph/Model/Graph.cs` (lines 40-55)

Already has a TODO on line 44. Removing 50 selected nodes with 1000 connections = 50k comparisons + individual `CollectionChanged` events.

**Fix:** Add `RemoveNodes(IEnumerable<Node>)` batch overload that collects affected connections first, then removes all in one pass.

---

## SUGGESTIONS

### 13. `Node` constructor sets Title to `GetType().Name`

Subclasses like `CommentNode` get `Title = "CommentNode"`. Fine for debugging but consumers may be surprised.

### 14. `IConnectionRouter.IsBezierRoute` couples router to renderer

The renderer special-cases `IsBezierRoute == true && screenPoints.Count == 4`. A typed route segment approach (line, bezier) would be cleaner but is a larger refactor.

### 15. Duplicate pen creation in `CanvasOverlay`

Identical pens for `previewValidPen` and `cuttingPen` are created in both the "Port highlights during connection draw" block (lines 235-242) and the "Connection draw preview" block (lines 267-275). Could be hoisted.

### 16. `NodeStyle` properties not consumed by `DefaultTemplates`

`HeaderFontSize`, `HeaderFontWeight`, `HeaderFontFamily`, `HeaderPadding`, `BodyMinHeight` exist in `NodeStyle` but are never applied in `DefaultTemplates.NodeTemplate` (lines 24-47). Consumers who set these properties will see no effect — silent feature gap.

---

## Priority Order

| Order | Issues | Theme |
|-------|--------|-------|
| 1st   | #3, #7 | API correctness — selection desync and hit-test ordering affect consumers directly |
| 2nd   | #1, #2 | Lifecycle — template pollution and port commit gap |
| 3rd   | #4, #5, #6, #8 | Performance — render allocations block the 500+ node target |
| 4th   | #10 | Documentation accuracy |
| 5th   | #9, #11, #12 | Scalability and cleanup |
| 6th   | #13-#16 | Nice-to-have improvements |
