# Port Improvements — Idea Menu (2026-04-13)

Brainstorming output, not yet a design. Options grouped by area. **★** marks best value-per-risk for this library's scope.

## Ground rules

- `PortFlow` (Input/Output) is a pure **semantic** property. A port's **position** on the node is pure **geometry**. The two are orthogonal and never cross: an output can live anywhere on the node, an input likewise. Every proposal below respects this.
- "Report, don't decide" from CLAUDE.md: any new behavior reports to consumer handlers and does not mutate domain state.

## Current state

- `Port`: Id, Owner, Name, `Flow`, `Position`, `AbsolutePosition`, `Style?`, `Label?`, `MaxConnections?` (metadata only), subclassable.
- `PortStyle`: fill, stroke, strokeWidth, shape (Circle/Square/Diamond/Triangle), size, label font/brush/offset/placement.
- Providers: `FixedPortProvider`, `DynamicPortProvider`.
- Interaction: drag from port, hover feedback, validator invoked during drag.

---

## Model / semantics

- **★ Port data types** — optional `DataType` (Type or string token) on `Port`, consumed by `IConnectionValidator` for type-compatible wiring. Biggest unlock for consumers building real node-graph tools. Open: generic `Type`, branded string, or open marker interface?
- **Multi-connection policy enforcement** — `MaxConnections` is metadata-only today. Option to let the library enforce (returning a `Result` failure) behind an opt-in flag, vs. keeping strictly advisory.
- **Port groups / categories** — group related ports (e.g. "transform", "material") for collapse/reorder/visual separation.
- **Required / optional marker** — consumer-consumable bool, used for validation and visual treatment (e.g. dashed outline on unconnected required port).

## Layout / positioning

- **★ Auto-layout on node resize** — ports currently hold a fixed `Position`. A `PortLayout` strategy (evenly spaced along an edge, weighted, grouped) that repositions ports when `Node.Width`/`Height` change. Likely the single biggest real-world pain point.
- **Anchor-based positioning** — `FixedPortProvider` positions are raw `Point`. A `PortAnchor` (edge + fractional offset 0..1) survives node resize without consumer math. Note: "edge" here is pure geometry, not a flow hint.
- **Dynamic port reordering** — let users drag ports along an edge to reorder; reported via a new handler.
- **Collapsible ports** — fold ports into a "+N more" stub when node is small or zoomed out.

## Interaction

- **★ Hover affordances on ports** — explicit `HoveredPort` state + an `IPortHoverHandler` (or flag on existing handler) enables tooltips, connect-ability preview, validation hints before drag.
- **Click-to-connect mode** — click source port, click target port, instead of drag. Accessibility and touch win.
- **Port context actions** — double-click / right-click on a port reported as a new interaction event (consumer decides: rename, disconnect all, edit type).
- **Magnetic snap during connection drag** — as the drag cursor nears a valid port, snap the preview endpoint. Visual + ergonomic.
- **Rubber-band disconnect** — grab an existing connection near its endpoint and drag it off. Currently only cut-line exists.

## Visual / styling

- **★ Validation-state visuals** — during connection drag, extend hover feedback to: "valid target" / "invalid target" / "already full" / "wrong type" states with distinct styling, driven by `IConnectionValidator` verdict. Small work, big UX.
- **Port shape extension point** — `PortShape` is an enum today. The deferred Task 8 from the perf pass (`IPortShapeRenderer`) lands here if consumers want custom shapes.
- **Port icon overlay** — render a glyph (arrow, dot, chain icon) centered on the port from a brush/geometry.
- **Connection-count badge** — tiny numeric badge on ports with >1 connection.
- **Theme tokens for port states** — resource keys for default/hover/valid/invalid/selected port brushes.

## Providers

- **Composite provider** — combine a `FixedPortProvider` (declared ports) with a `DynamicPortProvider` (ad-hoc). Currently exclusive.
- **Sticky dynamic ports** — `DynamicPortProvider` today reuses nearby ports; option to promote a frequently-used dynamic port into a permanent one.

## Testing / tooling

- **Port-level hit test in headless tests** — wrapper API for tests to say "click at port X" without coordinate math.

---

## Suggested first-pass bundle

**Port data types + validation-state visuals + auto-layout on resize.**

- They compose: type-mismatched drags drive the invalid-target visual; auto-layout keeps typed ports pinned to stable anchor points on resize.
- All three respect "report, don't decide".
- None of them touch `PortFlow` semantics or bake position into flow.
