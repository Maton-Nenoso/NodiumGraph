# Rendering Pipeline Reference

NodiumGraph uses hybrid rendering: nodes are real Avalonia controls populated via `DataTemplate`, while the grid, connections, port visuals, selection / hover borders, drag previews, the marquee, and the minimap are custom-drawn for performance. This page documents the render order, coordinate spaces, and hit-test behavior so you can predict what will appear in front of what, write routers and styles that behave correctly at all zoom levels, and diagnose "why is my node drawing over my port" surprises.

The primary drawing code lives in three places:

- `NodiumGraphCanvas.Render` draws the canvas-wide layers (background, grid, origin axes, connections).
- `NodeAdornmentLayer.Render` draws per-node decorations (selection / hover border, port shapes, port labels) inside each node's container, so they z-order correctly against neighbouring nodes.
- `CanvasOverlay.Render` draws pure drag/chrome (snap ghost, drag preview, cutting line, marquee, minimap). The overlay is an `Avalonia.Controls.Control` added as a visual child of the canvas with `ZIndex = int.MaxValue`, so Avalonia composites it above every node container.

Each node is hosted in an internal `NodiumNodeContainer` (a `Panel` subclass) with exactly two children: a `ContentPresenter` for the user's `NodeTemplate`, and a `NodeAdornmentLayer`. Avalonia renders children in order, so a given node's decorations paint over its own body but **under** any front node's body. That's how z-order overlap works correctly: a back-node port does not bleed across a foreground node.

## Render order

From bottom (drawn first) to top (drawn last):

| Layer | Renderer | Consumer-facing knobs |
|---|---|---|
| 1. Background | `NodiumGraphCanvas.Render` | `Background` |
| 2. Grid | `GridRenderer` | `ShowGrid`, `GridSize`, `GridStyle`, `MajorGridInterval` |
| 3. Origin axes | `GridRenderer.RenderOriginAxes` | `ShowOriginAxes` |
| 4. Connections | `ConnectionRenderer` (one call per visible connection) | `ConnectionRouter`, `DefaultConnectionStyle` |
| 5. Per node, in insertion order:<br>&nbsp;&nbsp;&nbsp;&nbsp;5a. Node body (`ContentPresenter` → `NodeTemplate`)<br>&nbsp;&nbsp;&nbsp;&nbsp;5b. Selection / hover border<br>&nbsp;&nbsp;&nbsp;&nbsp;5c. Port shapes (when `PortTemplate` is `null`)<br>&nbsp;&nbsp;&nbsp;&nbsp;5d. Port labels | 5a: Avalonia control tree<br>5b–5d: `NodeAdornmentLayer.Render` | `NodeTemplate`, `Node.IsSelected`, `Node.Style`, `PortTemplate`, `PortStyle`, `Port.Label`, theme keys |
| 6. Snap ghost | `CanvasOverlay.Render` | `SnapToGrid`, `ShowSnapGhost` |
| 7. Connection drag preview + port highlights | `CanvasOverlay.Render` | driven by `IConnectionValidator` |
| 8. Cutting line | `CanvasOverlay.Render` | right-drag gesture |
| 9. Marquee selection rectangle | `CanvasOverlay.Render` | left-drag on empty canvas |
| 10. Minimap | `MinimapRenderer` | `ShowMinimap`, `MinimapPosition` |

Layers 1–4 are drawn directly in `NodiumGraphCanvas.Render`. Layer 5 is the normal Avalonia visual-tree render of every `NodiumNodeContainer`, producing body + per-node decorations in a single pass per node. Layers 6–10 are drawn by the overlay on top of every node.

The per-node adornment layer draws in node-local world units under the container's `RenderTransform = ScaleTransform(ViewportZoom, ViewportZoom)`. That means port positions (`Port.Position`) are used directly — no manual `WorldToScreen` — and pen thicknesses are divided by `bucketedZoom` so stroke width stays visually constant as the user zooms.

Connection rendering performs viewport culling: the canvas computes the AABB of each route's points (inflated by stroke thickness) and skips connections whose bounds do not intersect the visible world-space rectangle.

## Coordinate spaces

NodiumGraph works with three coordinate spaces:

- **Screen coordinates** — raw Avalonia pixels inside the canvas control. These are what pointer events carry before the canvas translates them.
- **World coordinates** — infinite graph space, where `Node.X` / `Node.Y` and `Port.AbsolutePosition` live. `ViewportOffset` and `ViewportZoom` map world to screen.
- **Node-local coordinates** — relative to a node's top-left. `Port.Position` lives here; the canvas adds `(Owner.X, Owner.Y)` to reach world space (this is exactly what `Port.AbsolutePosition` returns).

The transform is implemented by `ViewportTransform` in `NodiumGraph.Controls`:

```
screen = world * ViewportZoom + ViewportOffset
world  = (screen - ViewportOffset) / ViewportZoom
```

Note that `ViewportOffset` is **not** a world-space pan, it is a screen-space translation applied after scaling. That also means zooming toward the cursor requires adjusting `ViewportOffset` in the opposite direction — the canvas handles this for you in its wheel / pinch handlers.

`ICanvasInteractionHandler.OnCanvasDoubleClicked` and `OnCanvasDropped` receive **world** coordinates, so you don't need to do this math yourself for those callbacks.

## Hit-test behavior

`NodiumGraphCanvas` implements `Avalonia.Rendering.ICustomHitTest` and returns `true` for every point inside its bounds. This is deliberate: without it, pointer and wheel events over empty canvas area would never reach the control (a `TemplatedControl` without a `ControlTemplate` is not otherwise hit-testable in those regions).

From there, the canvas dispatches the event internally in roughly this priority order:

1. **Port visuals** — highest priority, so dragging on a port always starts a connection even if a node would also be under the cursor.
2. **Node container controls** — standard Avalonia hit-testing via the control tree.
3. **Connection paths** — the canvas intersects the pointer position / cutting line against each connection's routed geometry (using bezier subdivision for `RouteKind.Bezier`).
4. **Empty canvas** — falls through to pan (space / middle mouse), marquee (Ctrl+left-drag or left-drag on empty space), or canvas-handler notifications.

`Node.IsCollapsed` suppresses port hit-testing: a collapsed node still draws its container, but its ports are hidden and none of them respond to pointer events.

## Performance characteristics per layer

- **Grid** — O(visible cells). `GridRenderer` culls to the canvas bounds; major-cell brushes are reused.
- **Origin axes** — two lines, constant cost.
- **Connections** — O(visible connections). Each call invokes the active `IConnectionRouter`. The default `BezierRouter` is constant-time per connection; viewport culling via AABB intersection drops off-screen connections entirely. If you replace the router with something expensive (e.g., an orthogonal path finder), cache per-connection inside the router or the canvas will call your `Route` method on every frame that involves the connection.
- **Node containers** — Avalonia's normal measure / arrange / render pipeline. Off-screen nodes still participate in layout but the canvas keeps the container cache tied to `Graph.Nodes`, so there is exactly one container per node for the lifetime of the graph.
- **Per-node adornments** — port drawing is O(total ports) but uses cached geometry (diamond / triangle shapes are keyed by a bucketed radius), cached pens, and bucketed text formatting for labels. Caches live on `NodiumGraphCanvas` and are shared across every `NodeAdornmentLayer`. Because adornments render per-node, a selection change invalidates only one node's layer, not the whole canvas — smaller dirty rect per edit, fewer wasted repaints during drag.
- **Connection preview / cutting line / marquee** — each is a single `DrawLine` or `DrawRectangle` call in the overlay; negligible.
- **Minimap** — O(total nodes). Only drawn when `ShowMinimap` is `true`.

The biggest single perf lever is `IConnectionValidator`: it runs on every pointer move during a connection drag. Keep it O(1) if at all possible.

## See also

- [NodiumGraphCanvas control reference](canvas-control.md)
- [Strategy interfaces reference](strategies.md) — custom routers and styles
- [Hybrid rendering](../4-explanation/hybrid-rendering.md) — design rationale for the layer split
- [Model reference](model.md)
