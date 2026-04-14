---
title: BezierRouter Port-Direction Awareness
tags: [spec, plan]
status: active
created: 2026-04-14
updated: 2026-04-14
---

# BezierRouter Port-Direction Awareness

`BezierRouter` currently pushes control points horizontally only — `cp1` and `cp2` share Y with start/end. Ports on the top or bottom edge of a node produce visually wrong routing: the curve shoots sideways before arcing back toward the target. This spec makes `BezierRouter` direction-aware so curves emit along whichever edge a port sits on.

## Scope

- Rewrite `BezierRouter.Route()` to push control points along each port's outward emission direction.
- Classification is derived geometrically from `Port.Position` vs `Owner` bounds. **No public API change.**
- `StepRouter` and `StraightRouter` are out of scope — separate concern if/when flagged.

## Non-goals

- `Port.Normal` / `EmissionDirection` explicit override for angled ports (YAGNI until requested).
- Span-based `Route()` perf refactor — own PR, only if allocations become measurable.
- Changes to `Port`, `IPortProvider`, `FixedPortProvider`, `DynamicPortProvider`.
- Changes to `ComputeRouteBounds` — already direction-agnostic via the convex-hull property of cubic beziers.

## Design

### 1. Emission direction classification

New `private static` helper in `BezierRouter`:

```csharp
private static Vector GetEmissionDirection(Port port)
```

Returns a unit vector pointing outward from the owner node along whichever edge the port is closest to. Classification compares the port's `Position` to the four edges of `Owner` (using `Owner.Width` / `Owner.Height`):

- `left   = Position.X`
- `right  = Owner.Width - Position.X`
- `top    = Position.Y`
- `bottom = Owner.Height - Position.Y`

Whichever distance is smallest wins:

| Winner | Direction |
| --- | --- |
| left   | `(-1, 0)` |
| right  | `(+1, 0)` |
| top    | `(0, -1)` |
| bottom | `(0, +1)` |

**Tie-break:** prefer horizontal (left/right) over vertical. Rationale:

1. Today's behavior is pure-horizontal, so this preserves the current visual for any ambiguous edge case.
2. Nodes are typically wider than tall, so horizontal emission is the statistically-better guess for interior or ambiguous ports.
3. `FixedPortProvider` ports authored at `(0, 0)` or `(Width, 0)` continue to behave exactly as they do now.

**Degenerate case:** if `Width == 0` and `Height == 0` (freshly constructed node before layout), all four distances tie → tie-break selects left-winner → `(-1, 0)`. Same horizontal behavior as today. Safe.

### 2. Generalized control-point formula

```csharp
private const double ControlPointFactor = 0.4;
private const double MinOffset = 30.0;

public IReadOnlyList<Point> Route(Port source, Port target)
{
    var start = source.AbsolutePosition;
    var end   = target.AbsolutePosition;

    var sourceDir = GetEmissionDirection(source);
    var targetDir = GetEmissionDirection(target);

    var dx = end.X - start.X;
    var dy = end.Y - start.Y;

    var sourceReach = Math.Abs(dx * sourceDir.X + dy * sourceDir.Y);
    var targetReach = Math.Abs(dx * targetDir.X + dy * targetDir.Y);

    var sourceOffset = Math.Max(sourceReach * ControlPointFactor, MinOffset);
    var targetOffset = Math.Max(targetReach * ControlPointFactor, MinOffset);

    var cp1 = new Point(start.X + sourceDir.X * sourceOffset,
                        start.Y + sourceDir.Y * sourceOffset);
    var cp2 = new Point(end.X   + targetDir.X * targetOffset,
                        end.Y   + targetDir.Y * targetOffset);

    return new[] { start, cp1, cp2, end };
}
```

Dot product is computed inline — no dependency on `Avalonia.Vector` static helpers. Two multiplies and an add.

### 3. Why the formula generalizes correctly

- **Current case** (both ports horizontal, source-right → target-left):
  `sourceDir=(+1,0)`, `targetDir=(-1,0)`. `sourceReach = targetReach = |dx|`. Collapses to `max(|dx|*0.4, 30)`. Identical to today.
- **Reverse horizontal** (source-right, target-right, target-node to the left — the "don't cross" case):
  `targetDir=(+1,0)`, `dx<0`. `targetReach = |dx|`. `cp2` pushed right — same swing-out behavior as today.
- **Both vertical** (bottom of node A → top of node B below it):
  `sourceDir=(0,+1)`, `targetDir=(0,-1)`. `sourceReach = targetReach = |dy|`. `cp1` pushed down, `cp2` pushed up. Clean S-curve.
- **Mixed** (source-right → target-top):
  Source pushes horizontally by `|dx|*0.4`; target pushes up by `|dy|*0.4`. Magnitudes are independent per port — the right behavior when each port's "reach" is along its own axis.
- **Zero delta, same-side ports** (both on top of the same node, exact same position — pathological):
  `sourceReach = targetReach = 0` → both clamp to `MinOffset = 30` → curve arches up by 30. No NaN, no degenerate point.

## Testing

### Existing tests — setup fixup required

All 4 existing tests in `BezierRouterTests.cs` construct `Node` instances without setting `Width` / `Height`. In production these are driven by Avalonia's layout pass (internal setter); in tests they default to `0.0`. Under the new geometric classification this would make every port ambiguous, so the existing tests need their setup updated to assign `Width` / `Height` directly via `InternalsVisibleTo` (already configured for `NodiumGraph.Tests` in `src/NodiumGraph/NodiumGraph.csproj:8`).

This is a **behavior-preserving refactor** — the current `Route()` implementation ignores node bounds entirely, so tests continue to pass against the current code with updated setup. Once `Route()` is rewritten, the same assertions still hold with the new formula.

Specific fixups:

- **Test 1 / Test 2** — give both nodes `Width = 100`, `Height = 50`. Source port `(100, 25)` becomes a right-edge port on nodeA; target port `(0, 25)` becomes a left-edge port on nodeB. Assertions unchanged.
- **Test 3 (`Offset_scales_with_distance`)** — source becomes right-edge of a sized nodeA; targets become left-edge of sized nodeNear/nodeFar. Reposition nodeFar so delta grows (`X = 600` rather than `500`).
- **Test 4 (`Route_right_to_left_does_not_cross`)** — source on **left** edge of nodeA (the right-hand node), target on **right** edge of nodeB (the left-hand node). The "facing each other under reversed layout" setup. `delta.X < 0`; both ports emit toward each other; existing assertions (`cp1.X <= start.X`, `cp2.X >= end.X`) still hold.

### New tests

Added to `BezierRouterTests.cs`:

1. **`Route_with_bottom_to_top_ports_pushes_control_points_vertically`** — source on node A's bottom, target on node B's top, node B below. Assert `cp1.X == start.X`, `cp1.Y > start.Y`; `cp2.X == end.X`, `cp2.Y < end.Y`.
2. **`Route_with_top_ports_on_same_y_produces_arc`** — both ports on top edges, zero `dy`. Assert both control points pushed up by exactly `MinOffset` (clamp).
3. **`Route_with_mixed_horizontal_and_vertical_ports_pushes_independently`** — source on right edge, target on top edge. Assert `cp1` horizontal-only, `cp2` vertical-only.
4. **`Route_classifies_corner_port_as_horizontal`** — port at `Position = (0, 0)` on a square node. Assert tie-break picks left: `cp1.X < start.X`, `cp1.Y == start.Y`.
5. **`Route_with_zero_size_owner_falls_back_to_horizontal`** — `Width = 0`, `Height = 0`. Assert horizontal behavior, no NaN / infinity in output.

**Target:** existing 4 → 9 bezier tests. Overall suite 395 → 400.

`ConnectionRendererTests.cs` requires no changes — geometry building and bounds computation are unaffected.

## Files touched

- `src/NodiumGraph/Interactions/BezierRouter.cs` — rewrite `Route()`, add `GetEmissionDirection()`, promote `ControlPointFactor` / `MinOffset` constants.
- `tests/NodiumGraph.Tests/BezierRouterTests.cs` — add 5 tests.
- `docs/plans/2026-04-14-bezier-port-direction-design.md` — this document.

## Files explicitly not touched

- `src/NodiumGraph/Model/Port.cs` — no API change.
- `src/NodiumGraph/Interactions/IPortProvider.cs`, `FixedPortProvider.cs`, `DynamicPortProvider.cs` — no change.
- `src/NodiumGraph/Interactions/StepRouter.cs`, `StraightRouter.cs` — out of scope.
- `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` `ComputeRouteBounds` — already convex-hull based, direction-agnostic.
- `src/NodiumGraph/Controls/ConnectionRenderer.cs` — geometry is direction-agnostic.

## Docs impact

None required. The user guide does not document `BezierRouter` internals; the visual improvement is transparent to consumers. If the deferred screenshot pass happens later, top/bottom port examples become viable — but that is not part of this change.
