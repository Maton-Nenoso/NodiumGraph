---
title: StepRouter port-direction awareness
tags: [plan, spec]
status: active
created: 2026-04-19
updated: 2026-04-19
---

# StepRouter port-direction awareness

Mirrors the port-direction work landed in BezierRouter on 2026-04-14 ([[2026-04-14-bezier-port-direction-design]]). StepRouter today always emits an H–V–H path through `midX`, which ignores port emission direction and produces awkward sideways egress for ports that sit on the top or bottom of their owner node.

## Goals

- StepRouter picks leg orientations based on each port's emission direction (horizontal vs vertical).
- Emission-direction resolution is shared with BezierRouter — single source of truth.
- No pathfinder, no obstacle avoidance. Router stays a geometric primitive.
- No change to `RouteKind`, `IConnectionRouter` surface, or consumer-visible types.

## Non-goals

- S-curve / detour handling for "partner sits behind emission" configurations.
- Direction awareness for `StraightRouter` (straight lines ignore emission by definition).
- Obstacle-aware routing or port docking.

## Design

### Shared emission-direction helper

New file `src/NodiumGraph/Interactions/PortEmissionDirection.cs`:

```csharp
internal static class PortEmissionDirection
{
    public static Vector Resolve(Port port) { /* ported from BezierRouter.GetEmissionDirection */ }
}
```

`internal` — the helper stays off the public surface. BezierRouter's private `GetEmissionDirection` is deleted and replaced with a call to `PortEmissionDirection.Resolve`. Logic is byte-identical; existing BezierRouter tests cover it end-to-end.

### StepRouter routing rules

Classify each port by its resolved direction vector: **H** when `abs(dir.X) == 1`, **V** otherwise.

| Source | Target | Path |
|--------|--------|------|
| H | H | `[start, (midX, start.Y), (midX, end.Y), end]` |
| V | V | `[start, (start.X, midY), (end.X, midY), end]` |
| H | V | `[start, (end.X, start.Y), end]` (L-bend) |
| V | H | `[start, (start.X, end.Y), end]` (L-bend) |

**Aligned shortcuts** (ε = 0.001):

- H + H and `|start.Y − end.Y| < ε` → `[start, end]`.
- V + V and `|start.X − end.X| < ε` → `[start, end]`.

### Edge cases & invariants

- **Degenerate bend collapse.** Mixed-emission on an aligned row/column places the bend on `start` or `end`. StepRouter collapses consecutive duplicates (within ε) before returning so downstream consumers never see zero-length segments.
- **Zero-size node / port declared outside owner.** Inherited from `Resolve`; no special handling needed.
- **Direction vector cardinality.** `Resolve` only returns `(±1, 0)` or `(0, ±1)`. The H/V classification (`abs(dir.X) == 1` ⇒ H, else V) relies on this invariant.
- **"Partner behind emission" configs.** Leading leg may contradict emission direction (e.g. both ports emit →, target sits left of source). Accepted as scope limit — matches BezierRouter's cutoff for detours.
- **RouteKind stays `Polyline`.** Renderer is unaffected.
- **Allocation profile.** No heap allocations beyond the returned list; no LINQ; no virtual calls.

## Testing

Added to `tests/NodiumGraph.Tests/StepRouterTests.cs`:

- Per-combination (H+H, V+V, H+V, V+H) — ~6 tests covering midX/midY paths and both L-bend corners.
- Aligned shortcuts — 2 tests (aligned row + both-H, aligned column + both-V).
- Degenerate collapse — 2 tests (mixed emission on aligned row, on aligned column).
- Partner-behind — 2 tests pinning accepted degradation so future regressions are explicit.

~10 new unit tests. Pure geometry — no Avalonia headless runtime required.

## Out of scope

- Emission-direction awareness beyond the "which edge is closest" heuristic (e.g. manual port-direction override).
- Detour / escape-segment logic for partner-behind or obstacle scenarios.
- Port-direction awareness in StraightRouter.
