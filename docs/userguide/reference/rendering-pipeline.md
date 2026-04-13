# Rendering Pipeline Reference

NodiumGraph uses hybrid rendering: nodes are real Avalonia controls populated via `DataTemplate`, while the grid, connections, port visuals, selection / hover borders, drag previews, the marquee, and the minimap are custom-drawn for performance. This page documents the render order, coordinate spaces, and hit-test behavior so you can predict what will appear in front of what, write routers and styles that behave correctly at all zoom levels, and diagnose "why is my node drawing over my port" surprises.

The primary drawing code lives in `NodiumGraphCanvas.Render` and in `CanvasOverlay.Render`. The overlay is a regular `Avalonia.Controls.Control` added as a visual child of the canvas with `ZIndex = int.MaxValue`, which is how it ends up on top of the Avalonia-rendered node containers.

## Render order

From bottom (drawn first) to top (drawn last):

| Layer | Renderer | Consumer-facing knobs |
|---|---|---|
| 1. Background | `NodiumGraphCanvas.Render` | `Background` |
| 2. Grid | `GridRenderer` | `ShowGrid`, `GridSize`, `GridStyle`, `MajorGridInterval` |
| 3. Origin axes | `GridRenderer.RenderOriginAxes` | `ShowOriginAxes` |
| 4. Connections | `ConnectionRenderer` (one call per visible connection) | `ConnectionRouter`, `DefaultConnectionStyle` |
| 5. Node containers | Avalonia control tree instantiated from `NodeTemplate` | `NodeTemplate`, per-type `<DataTemplate>` |
| 6. Selection / hover borders | `CanvasOverlay.Render` | `Node.IsSelected`, `Node.Style`, theme keys |
| 7. Snap ghost | `CanvasOverlay.Render` | `SnapToGrid`, `ShowSnapGhost` |
| 8. Port visuals | `CanvasOverlay.Render` (when `PortTemplate` is `null`) | `PortTemplate`, `PortStyle`, theme keys |
| 9. Port labels | `CanvasOverlay.Render` | `Port.Label`, `PortStyle.LabelPlacement` |
| 10. Connection drag preview | `CanvasOverlay.Render` | driven by `IConnectionValidator` |
| 11. Cutting line | `CanvasOverlay.Render` | right-drag gesture |
| 12. Marquee selection rectangle | `CanvasOverlay.Render` | left-drag on empty canvas |
| 13. Minimap | `MinimapRenderer` | `ShowMinimap`, `MinimapPosition` |

Layers 1-4 are drawn directly in `NodiumGraphCanvas.Render`. Layer 5 is the normal Avalonia visual-tree render for every `ContentControl` that the canvas instantiates per node. Layers 6-13 are drawn by the overlay, which Avalonia composites on top of the node containers thanks to its `ZIndex`.

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
- **Overlay visuals** — port drawing is O(total ports) but uses cached geometry (diamond / triangle shapes are keyed by a bucketed radius), cached pens (`IConnectionStyle` getters are reference-compared), and bucketed text formatting for labels. Expect per-frame cost to be dominated by ports and labels when you have hundreds of nodes.
- **Connection preview / cutting line / marquee** — each is a single `DrawLine` or `DrawRectangle` call; negligible.
- **Minimap** — O(total nodes). Only drawn when `ShowMinimap` is `true`.

The biggest single perf lever is `IConnectionValidator`: it runs on every pointer move during a connection drag. Keep it O(1) if at all possible.

## See also

- [NodiumGraphCanvas control reference](canvas-control.md)
- [Strategy interfaces reference](strategies.md) — custom routers and styles
- [Hybrid rendering](../explanation/hybrid-rendering.md) — design rationale for the layer split
- [Model reference](model.md)
