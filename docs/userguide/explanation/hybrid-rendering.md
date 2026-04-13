# Hybrid Rendering

NodiumGraph renders a single canvas using two completely different strategies. Nodes are real Avalonia controls, rendered through the regular `DataTemplate` system — which gives you bindings, animations, focus, styling, and everything else Avalonia ships with. Connections, the grid, the selection marquee, the drag preview, and the port visuals are custom-drawn — the canvas owns a `DrawingContext` and paints them directly. This essay explains why a single control uses both, and what it means for you when you build against the library.

## Two very different jobs

A node is a chunk of visible domain data — a header, some text, maybe a button or a slider, probably some per-instance colors. Every real app wants to put arbitrary visuals into its nodes, and Avalonia already has a world-class system for that: `DataTemplate`. If NodiumGraph ignored the DataTemplate system and custom-drew nodes, it would reinvent theming, animations, text layout, focus, input-method support, and accessibility — and then argue with Avalonia about which one owns each of them.

A connection is the opposite: a smooth curve between two points, drawn thousands of times per frame, looking exactly the same as every other connection, and useful chiefly because it renders fast. Using an Avalonia `Path` per connection would be correct on paper. It would also allocate thousands of `Geometry`, `Path`, and visual-tree objects, each participating in layout and hit-testing and template resolution on every zoom. By the time you hit 500 connections, layout passes alone would eat the frame budget.

So the library splits the problem down the middle:

| Element | How it's rendered | Why |
|---|---|---|
| Nodes | Avalonia `DataTemplate` → `ContentControl` children | Need full Avalonia templating, bindings, focus, styling |
| Ports | Custom-drawn in the canvas overlay | Tiny, numerous, pure shapes with no child content |
| Connections | Custom-drawn `Geometry` + `Pen` per frame | Smooth curves, thousands per graph, read-only |
| Grid | Custom-drawn dots or lines | Background, huge, no interaction |
| Selection marquee | Custom-drawn rectangle | Transient, painted once per pointer-move |
| Drag preview | Custom-drawn with the same router | Must match the final connection 1:1 |
| Snap ghost | Custom-drawn outline | Only visible during a drag |

## The rendering order

Every render pass runs bottom-up through seven layers:

1. **Grid** — `GridRenderer` paints dots or lines behind everything else.
2. **Connections** — `ConnectionRenderer` asks the active `IConnectionRouter` for each connection's path and draws it using the canvas's pen cache.
3. **Drag preview** — the in-progress connection drag, rendered with the same router so the live preview matches the final connection exactly.
4. **Node containers** — each `Node` has a `ContentControl` child managed by the canvas, rendered by Avalonia's regular layout pass.
5. **Port visuals** — tiny filled shapes in the overlay, on top of the nodes so they stay visible through node backgrounds.
6. **Selection marquee** — the rubber-band rectangle.
7. **Validation feedback** — the "this port is a valid/invalid target" indicator during a connection drag.

The ordering is deliberate: connections draw *behind* nodes so a node can visually occlude a wire routed behind it, but ports draw *above* nodes so they always stay grabbable. Selection and validation sit on top so they're never hidden by the content they describe.

## What this costs you

Because nodes are real Avalonia controls, everything you already know about Avalonia works inside a node template. `{Binding ...}` works. `Styles` selectors work. `DataTrigger` works. `Focus` works. `ContextMenu` works. `ToolTip` works. You don't have to learn a NodiumGraph-specific templating language — there isn't one.

Because the other layers are custom-drawn, you don't have any of that power over them. You can't drop a `Button` onto a connection. You can't use an Avalonia `Style` to change the selection marquee. You can't hit-test a grid cell with `Classes.Add("hovered")`. What you can do is tell the canvas *what* to draw — through `IConnectionStyle`, `IConnectionRouter`, and the resource dictionary — and trust it to paint fast.

For grid and connections this trade is basically free: nobody misses the ability to apply a `DataTrigger` to a bezier curve. For ports, the trade is a little tighter — you occasionally wish you could hit-test a port like a real control — and the library leaves a door open in the form of the `PortTemplate` styled property, which lets you opt ports into a custom Avalonia control tree if you really need one. Most apps don't.

## What it unlocks

The win is performance. The project's non-functional target is smooth pan and zoom with 500+ nodes and 1000+ connections, with no reflection in hot paths (so the library stays AOT-compatible). That target is only reachable because the hot path is custom-drawn, and the hot path is only allowed to be custom-drawn because it only paints things that don't need to be interactive children of the visual tree. Nodes — the things users click, focus, and style — get the full Avalonia treatment. Everything else gets the fast treatment.

The second win is composability. Because the connection layer is a pure `IConnectionRouter` → `Pen` → `DrawingContext` pipeline, a custom router is a ten-line class that returns four points. A custom style is a ten-line class that returns a brush and a width. You write them without touching the Avalonia visual tree, and the canvas calls you once per frame per connection. If you try to route connections using `Path` elements, that simplicity disappears.

## In short

- **Nodes use DataTemplates** so the user's app gets the full Avalonia toolbox for the parts users actually interact with.
- **Everything else uses custom drawing** so the library stays fast at the scale real diagrams live at.
- **The two strategies are stitched together in one render pass**, in a fixed order, with each layer doing exactly the job it's good at.
- **The split is invisible most of the time** — you template your nodes, implement a strategy or two, and the library decides which rendering path everything takes.
