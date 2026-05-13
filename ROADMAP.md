# NodiumGraph Roadmap

Status: pre-1.0 (2026-05-12). Breaking changes are still free; the policy flips at 1.0.

This roadmap is the forward-looking companion to `README.md`. Priorities reflect the value/difficulty matrix in `docs/plans/2026-04-13-port-improvements-prioritized.md` and the gaps surfaced during user-guide authoring.

## Shipped (highlights)

- Hybrid rendering pipeline — Avalonia DataTemplate nodes + custom-rendered connections, grid, minimap, marquee, cutting line, origin axes
- Three routers: `StraightRouter`, `BezierRouter`, `StepRouter` — all share `PortEmissionDirection` for edge-aware emission
- Five endpoint decorations with inset-aware stroke: arrow, bar, circle, diamond, none
- Unified selection model: `Graph.SelectedItems` with filtered `SelectedNodes` / `SelectedConnections` views; click, ctrl-click, and marquee all pick connections; selection halo render pass
- Cascading remove: `Graph.RemoveNode` drops dependent connections and selection entries
- Connection geometry cache + `ConnectionHitTester` (StrokeContains / FillContains), invalidated on node move, router/style swap, theme change
- Per-node `NodeAdornmentLayer` for correct z-order of borders, ports, and port labels
- `Port.DataType` opaque token + `DefaultConnectionValidator` (self / same-owner / Flow / DataType rules)
- `IGraphInteractionHandler` for unified mixed-selection delete
- Full Diátaxis user guide (tutorial + 16 how-to recipes + 6 reference pages + 3 explanation essays) backed by `samples/GettingStarted/` and `samples/NodiumGraph.Sample/`

## Near-term — Port UX 1.0 bundle

The coherent release queued in the prioritized ideas doc. Goal: move the canvas from "primitives" to "usable node editor" without touching routing, undo, or flow semantics.

- [ ] **Auto-layout on resize** — `PortLayout` strategy interface (even-spaced along edge, weighted, grouped). `FixedPortProvider.layoutAware` only snaps fixed positions to the boundary today; resizable nodes still need consumer math.
- [ ] **Anchor-based positioning** — `PortAnchor` (edge + 0..1 fractional offset). Pairs with auto-layout; eliminates the most common consumer arithmetic.
- [ ] **Validation verdict** — extend `IConnectionValidator` from `bool` to an enum (`Valid` / `Invalid` / `AlreadyFull` / `WrongType`) and surface state-specific visuals during drag.
- [ ] **`HoveredPort` state + `IPortHoverHandler`** — foundational for tooltips, pre-drag hints, and several Tier A items.
- [ ] **Required / optional port marker** — `bool` on `Port`; dashed outline on unconnected required ports.

## Medium-term — Tier A ergonomic wins

- [ ] **Magnetic snap during connection drag** — preview snaps to nearby valid port within a threshold.
- [ ] **Rubber-band disconnect** — grab an existing connection near an endpoint and drag it off. Interacts with the cut-line; needs an explicit reconnect/delete handler contract.
- [ ] **Click-to-connect mode** — touch/accessibility alternative to drag. Composes with `HoveredPort`.

## Longer-term — Tier B / C

Polish, niche, and power-user features. Order is rough; pick based on consumer demand.

- Port context actions (double/right-click events on a port)
- Dynamic port reordering along an edge
- Port groups / categories
- Connection-count badge per port
- Port icon overlay (geometry-based glyph)
- `MaxConnections` enforcement (currently advisory metadata only)
- Composite port provider (`FixedPortProvider` + `DynamicPortProvider` on the same node)
- Sticky dynamic ports (promote a frequently-used dynamic port into a permanent declaration)
- Theme resource tokens for port states (default / hover / valid / invalid)
- `IPortShapeRenderer` extension point for custom port shapes
- Collapsible ports ("+N more")

## Documented API gaps

Small PRs that would close caveats called out in the how-to recipes during user-guide authoring.

- [ ] Per-connection `IConnectionStyle` — currently canvas-wide via `DefaultConnectionStyle`. Per-connection style is the main blocker for consumers wanting mixed visual treatments in one graph.
- [ ] `IsPanEnabled` / `IsZoomEnabled` gating flags — pan and zoom gestures are hardcoded; locking zoom today requires `MinZoom == MaxZoom`.
- [ ] AXAML-bindable canvas-level selection surface — selection currently lives only on `Graph.SelectedItems` / `SelectedNodes`, not on a `NodiumGraphCanvas` property.

## User guide

- [ ] Replace screenshot placeholders with real captures (deferred during the original Diátaxis pass).

## Toward 1.0

Once the Port UX bundle and at least the per-connection style gap ship:

- API stabilization sweep — audit public names, defaults, and AXAML surface.
- Real-world consumer integration on a non-sample application.
- Performance pass against the stated bar: smooth pan/zoom with 500+ nodes and 1000+ connections.
- AOT publish smoke test for the library and `samples/GettingStarted/`.

## Non-goals

Per `CLAUDE.md` / `AGENTS.md`, NodiumGraph ships primitives only. The following are explicitly out of scope and consumer responsibility:

- Undo / redo
- Layout algorithms (auto-arrange, force-directed, hierarchical, etc.)
- Serialization (graph load/save)
- Context menus and keyboard shortcut catalog
- Search / filter
- Connection validation rules beyond the structural defaults
- Node content and appearance (consumer's DataTemplate)

## Source documents

- `docs/plans/2026-04-13-port-improvements-ideas.md` — full brainstorm (21 ideas, grouped by area)
- `docs/plans/2026-04-13-port-improvements-prioritized.md` — value/difficulty matrix and recommended bundle
- `docs/userguide/` — current end-user documentation
