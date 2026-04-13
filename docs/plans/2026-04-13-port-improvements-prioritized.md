# Port Improvements — Prioritized (2026-04-13)

Companion to `2026-04-13-port-improvements-ideas.md`. Two passes: end-user value, then difficulty × library-ease impact.

## Pass 1: End-user value (high → low)

### Tier S — transformative for real node-graph UX
1. **Port data types + type-compatible validation** — the unlock that makes NodiumGraph feel like a real tool (Unreal BP, Shader Graph, n8n). Without it, every consumer reinvents it.
2. **Auto-layout on node resize** (`PortLayout` strategy) — resize is ubiquitous; fixed `Point` positions are the single biggest paper cut.
3. **Validation-state visuals during drag** — "this will work / won't work" feedback is what separates toy editors from production ones.

### Tier A — strong ergonomic wins
4. **Magnetic snap during connection drag** — feels magical, low conceptual cost for users.
5. **Hover affordances + `HoveredPort` state** — enables tooltips, pre-drag hints; foundational for several others.
6. **Rubber-band disconnect** — users expect this from every modern editor.
7. **Anchor-based positioning** (edge + 0..1 offset) — invisible to end users directly, but makes resize/auto-layout actually usable.

### Tier B — nice polish
8. **Click-to-connect mode** — touch/accessibility; niche but cheap.
9. **Required / optional markers** — visible cue for "you forgot to wire this".
10. **Port context actions** (dbl/right-click event) — enables consumer menus.
11. **Dynamic port reordering** — noticeable in list-style nodes.
12. **Port groups / categories** — matters once node port counts grow.
13. **Connection-count badge** — small but readable signal.
14. **Port icon overlay** — branding/visual richness.

### Tier C — power-user / edge
15. **Collapsible ports ("+N more")** — only matters for dense graphs.
16. **Multi-connection policy enforcement** — most consumers already gate this in their validator.
17. **Composite provider** (fixed + dynamic) — unlocks hybrid nodes; niche.
18. **Sticky dynamic ports** — clever but narrow.
19. **Theme tokens for port states** — consumers can already restyle.
20. **Port shape extension point** (`IPortShapeRenderer`) — few will write custom shapes.
21. **Port-level hit-test test helper** — internal tooling, not end-user.

---

## Pass 2: Difficulty × Library-ease impact

Legend — Difficulty: **XS / S / M / L**. Ease impact: **+** helps, **++** big help, **−** adds surface, **0** neutral.

| # | Feature | Diff | Ease | Notes |
|---|---|---|---|---|
| 1 | Port data types | **S** | **++** | One nullable prop + validator hook. Open question is just the type shape (Type vs token) — pick `object? DataType` + equality delegate to stay AOT-clean. |
| 2 | Auto-layout on resize (`PortLayout`) | **M** | **++** | Needs a strategy interface + hooking `Node.Width/Height` change. Pairs with #7. |
| 3 | Validation-state visuals | **S** | **+** | Extends existing hover/validator path. Mostly rendering + a verdict enum on `IConnectionValidator`. Adds one enum to public API. |
| 4 | Magnetic snap during drag | **S** | **0** | Pure interaction layer; no API surface growth. |
| 5 | Hover affordances / `HoveredPort` | **S** | **+** | Adds one handler interface; foundational for 3, 4, 9, 10. |
| 6 | Rubber-band disconnect | **M** | **0** | Needs hit-testing connection endpoints + a reconnect/delete handler contract. Careful: interacts with existing cut-line. |
| 7 | Anchor-based positioning | **S** | **++** | Small struct (`PortAnchor`); huge simplification for consumers doing math today. Do alongside #2. |
| 8 | Click-to-connect mode | **S** | **+** | State machine addition; composes with #5. |
| 9 | Required/optional marker | **XS** | **+** | Bool on `Port` + validator/visual consumption. Trivial. |
| 10 | Port context actions | **XS** | **+** | One more handler method. Trivial. |
| 11 | Dynamic port reordering | **M** | **0** | New drag mode + reorder-reported handler; edge cases around anchor recalculation. |
| 12 | Port groups / categories | **M** | **+** | Model change (group id on Port) + layout awareness. Ripples into #2. |
| 13 | Connection-count badge | **XS** | **0** | Pure render; reads existing graph state. |
| 14 | Port icon overlay | **S** | **0** | Extends `PortStyle`; AOT-safe with `Geometry`. |
| 15 | Collapsible ports | **L** | **−** | Large: layout, hit-test, hidden-port connection routing, zoom coupling. High risk for the value. |
| 16 | MaxConnections enforcement | **XS** | **+** | One opt-in flag + a `Result` failure path. |
| 17 | Composite provider | **S** | **+** | Decorator over the two providers. |
| 18 | Sticky dynamic ports | **S** | **0** | Promotion API on `DynamicPortProvider`. |
| 19 | Theme tokens | **S** | **+** | Resource dictionary + doc. Zero logic. |
| 20 | `IPortShapeRenderer` | **M** | **0** | Deferred Task 8 from perf pass. Adds extension point, touches render hot path. |
| 21 | Port-level test helper | **XS** | **+** | Test-only; no runtime cost. |

---

## Recommendation

The ideas doc's first-pass bundle holds up: **#1 + #3 + #2 (with #7 folded in)**. Add **#9 (required marker)** and **#5 (HoveredPort)** — both are XS/S and compose directly with the bundle. That's one coherent release that raises the library from "canvas primitives" to "usable node editor" without touching routing, undo, or flow semantics.
