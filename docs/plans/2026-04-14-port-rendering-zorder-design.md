---
title: Per-Node Port Rendering (z-order fix)
tags: [spec, plan]
status: active
created: 2026-04-14
updated: 2026-04-14
---

# Per-Node Port Rendering (z-order fix)

## Problem

When two nodes visually overlap, the ports (and port labels, selection border) of the logically-behind node bleed **on top of** the foreground node. Visible in practice as red port circles drawn over another node's body text.

## Root cause

`CanvasOverlay.Render()` draws ports, port labels, and selection borders in a **single global pass** after all node containers have rendered. Per `src/NodiumGraph/Controls/CanvasOverlay.cs:93-234`, the overlay iterates every node's every port and draws them onto the shared canvas `DrawingContext`. Since this pass runs after `base.Render()` of `NodiumGraphCanvas` (which renders all node containers), any port drawn by the overlay is painted on top of every node body, regardless of which node it belongs to. The same mechanism affects port labels and the selection border.

`NodiumGraphCanvas` does not maintain explicit z-index; node containers are ordered by insertion into the `Children` collection (`NodiumGraphCanvas.cs:1258`, `AddNodeContainer`). That ordering is correct for node bodies — it breaks only because port decorations bypass the per-container render scope.

## Goal

Each node's decorations (ports, port labels, selection border) render **with** that node's container, so Avalonia's natural visual-tree traversal produces correct z-order overlap: front nodes fully cover back nodes, including the back node's port decorations.

## Non-goals

- No new public API surface. `NodeTemplate` contract stays the same from the consumer's perspective.
- No change to hit-testing (`ResolvePort` / `ResolvePortWithProvider` already iterate `Graph.Nodes` directly and are render-independent).
- No change to connection rendering — connections are canvas-level, not per-node.
- No change to `BezierRouter`, `ConnectionRenderer`, grid, origin axes.
- No switch from custom `DrawingContext` drawing to per-port Avalonia controls. The "custom-rendered for performance" principle is preserved.
- No move of canvas-chrome overlays (validation feedback, drag preview, cutting line, marquee, minimap). These are drag-scoped or global chrome that legitimately draws above everything.

## Architecture

### New internal types

**`NodiumNodeContainer : Panel`** — replaces the bare `ContentControl` currently constructed per-node inside `NodiumGraphCanvas.AddNodeContainer`. Two visual children:

- **child[0]** — `ContentPresenter` applying the consumer's `NodeTemplate` with the `Node` as `DataContext`. This is the node body.
- **child[1]** — `NodeAdornmentLayer`. The per-node decoration control.

`MeasureOverride` returns the `ContentPresenter`'s desired size. `ArrangeOverride` arranges both children to the full bounds so the adornment layer overlays the content at identical extents.

Because Avalonia's visual tree traverses children in order, child[1] draws **after** child[0]. Ports are drawn over **their own node's body** but under **any next node's body** (because the next node is a sibling `NodiumNodeContainer`, rendered entirely after this one). That is the fix.

**`NodeAdornmentLayer : Control`** — internal, non-hit-testable (`IsHitTestVisible = false`). Holds references to the owning `NodiumGraphCanvas` and the `Node`. Overrides `Render(DrawingContext)` to draw, in node-local coordinates:

1. Selection border (if `canvas.Graph.SelectedNodes.Contains(node)`)
2. Port shapes (dispatched on `Port.Style.Shape`)
3. Port labels

Hit-testing is disabled because `NodiumGraphCanvas` handles all pointer input centrally and resolves ports via `ResolvePort` on its own inputs. The adornment layer is purely visual.

### `NodiumGraphCanvas` changes

- `AddNodeContainer` constructs a `NodiumNodeContainer` instead of a `ContentControl`. The container's position-on-canvas transform (current world-to-canvas scheme) is applied to the container itself, unchanged.
- `_nodeContainers` dictionary's value type changes from `ContentControl` to `NodiumNodeContainer`. All callers update.
- Any code that currently calls `_overlay.InvalidateVisual()` because of a per-node-decoration change instead calls `_nodeContainers[node].InvalidateVisual()` (or its adornment-layer child). Survey needed during planning — the current CanvasOverlay invalidation fan-out will be narrowed.

### `CanvasOverlay` changes

Remove rendering code for:
- Port shapes
- Port labels
- Selection border

Keep rendering code for:
- Validation feedback ring (drag-scoped, one target port at a time — no overdraw concern because there is only one target during a drag)
- Connection drag preview
- Cutting line
- Marquee rectangle
- Minimap

The overlay's footprint shrinks substantially. If after the move the overlay's only drag-scoped rendering is validation feedback, consider renaming / reorganizing in a follow-up pass — **not in this change**.

## Data flow

- Consumer mutates `Node` position → existing invalidation flows reposition `NodiumNodeContainer` on the canvas — unchanged.
- Consumer mutates port list / `Port.Style` / `Port.Label` → **new:** invalidate the affected `NodeAdornmentLayer` only. Today this triggers a full overlay invalidation.
- Consumer mutates selection → invalidate only the affected nodes' adornment layers (newly selected + newly deselected).
- Consumer mutates `Node.Width` / `Height` → container `MeasureOverride` picks up the new desired size on the next layout pass. Both children rearranged.
- Zoom changes → the adornment layer reads `_canvas.ZoomLevel` for zoom-stable stroke widths, same pattern the overlay uses today. Zoom-change invalidates all containers (or, if cheap, the canvas triggers an explicit invalidation sweep).

## Invalidation granularity — a free win

**Today:** any port-level change → `CanvasOverlay.InvalidateVisual()` → re-renders every port on every node, every label, every selection border, plus the marquee and minimap on every repaint.

**Under this change:** a port-level change on node N → `_nodeContainers[N].InvalidateVisual()` → re-renders only node N's decorations. The marquee, minimap, drag preview, and validation feedback no longer get dragged into the repaint.

This is a byproduct of the fix, not a motivation — but it will noticeably reduce repaint cost in edit-heavy workflows.

## Hit-testing (unchanged)

`ResolvePort` and `ResolvePortWithProvider` iterate `Graph.Nodes` and each node's port provider directly. They do not touch render code. Moving port rendering into per-node containers has no effect on hit-testing.

This also means clicking a port still works even if the port is visually covered by another node — the centralized hit-test still finds the back-node port. Whether that's desirable is a separate question (hit-test z-order would need a fix of its own). **Not in scope for this change** — this change fixes the visual bleed only.

## Customizable port shapes

This architecture is orthogonal to shape customization. Today the port shape is an enum dispatched inside the draw method. Under this change the same dispatch lives in `NodeAdornmentLayer.Render()`. Any future strategy for custom shapes (drawable `IPortStyle.Draw(ctx, port)`, `IPortRenderer` strategy interface, per-port delegate) drops in without another architectural move.

The only shape-customization approach this architecture does NOT fit is "per-port Avalonia controls with DataTemplates" — and that's explicitly rejected for perf reasons regardless.

## Testing

### Existing tests to preserve or migrate

- **Router tests, hit-test tests, connection-validation tests, graph-model tests** — unaffected. Pass unchanged.
- **Any `CanvasOverlay` test asserting on port/label/selection-border output** — migrates to target `NodeAdornmentLayer`. Survey during planning.

### New tests

- **Structural test:** after `AddNode`, the `_nodeContainers[node]` is a `NodiumNodeContainer` with exactly two children — a `ContentPresenter` followed by a `NodeAdornmentLayer`. Asserts the visual tree shape.
- **Adornment-layer hit-testing:** `NodeAdornmentLayer.IsHitTestVisible` is `false`. Guards against a future refactor accidentally enabling pointer routing on it and confusing the centralized `ResolvePort` path.
- **Invalidation granularity:** changing a port style on node A invalidates node A's container but does NOT invalidate the canvas overlay. Implementation TBD during planning — may need a small test helper that tracks `InvalidateVisual` calls.
- **Removal:** after `RemoveNode`, the container (and its adornment layer) is removed from `_nodeContainers` and from `Children`.

### Manual verification

The bug itself cannot be asserted in headless Avalonia tests without baseline visual diffing. Manual repro:

1. In `samples/NodiumGraph.Sample` (or a purpose-built test scene), place two nodes so they overlap.
2. Drag one node to move it partially behind the other.
3. Confirm the back node's ports are covered by the front node's body — no red-circle bleed-through like the screenshot that motivated this change.
4. Confirm dragging ports from the back node still works (hit-test unchanged), even when the port is visually hidden.

## Risks

1. **Panel-subclass layout subtlety.** `MeasureOverride` / `ArrangeOverride` on a custom Panel can interact badly with parent `Canvas` positioning. Need to verify the container's desired size matches the content and that the canvas's absolute-positioning transforms are applied to the container (not double-applied to children).
2. **Per-node selection invalidation wiring.** The current code path that fires on selection change invalidates the overlay globally. That needs to become per-affected-node. Simple in principle; easy to miss a wiring point — planning phase will enumerate every call site.
3. **Zoom-level change invalidation.** A zoom change today invalidates one thing (the overlay). After this change, it needs to invalidate every adornment layer (because stroke widths depend on zoom). A simple sweep over `_nodeContainers.Values` works; cost is proportional to visible nodes, which is fine.
4. **Resource access from the adornment layer.** Today `CanvasOverlay.Render()` reads zoom, default styles, theme brushes directly from `_canvas`. `NodeAdornmentLayer` will do the same — the internal `NodiumGraphCanvas` reference is the intended coupling, not a leak.

## Out of scope (intentionally deferred)

- **Hit-test z-order.** If a back-node port is visually covered but still clickable, that's inconsistent. Fixing it requires iterating nodes in reverse z-order in `ResolvePort`. Not the bug we're fixing. Separate change.
- **Port customization strategy.** Interface/enum extensions for custom port shapes are explicitly out of scope. The move to per-node rendering is a prerequisite and enabler; the customization design is its own PR.
- **Canvas overlay rename / reorganization.** After this change, the overlay is mostly drag-scoped chrome. Consider renaming or splitting in a follow-up. Not this change.
- **StepRouter / StraightRouter direction awareness** (ongoing roadmap item for the router, unrelated to rendering).
