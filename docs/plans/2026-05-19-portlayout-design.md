---
title: PortLayout — auto-distribute fractions for declarative ports
tags: [plan, spec]
status: active
created: 2026-05-19
updated: 2026-05-19
---

# PortLayout — auto-distribute fractions for declarative ports

Today a consumer declaring three ports on the same edge in AXAML must hand-pick each `Fraction`:

```xml
<ng:PortDefinition Name="In1" Edge="Left" Fraction="0.25"/>
<ng:PortDefinition Name="In2" Edge="Left" Fraction="0.5"/>
<ng:PortDefinition Name="In3" Edge="Left" Fraction="0.75"/>
```

If a `Fraction` is omitted, it defaults to `0.5` — three ports stack on top of each other at the midpoint. This is the single largest paper cut left in declarative port topology after [[2026-05-14-declarative-axaml-ports-design]] and [[2026-05-13-anchor-based-port-positioning-design]].

The mental model: **omitting `Fraction` declares intent to auto-layout**. The library distributes auto ports evenly along their edge using a single built-in algorithm, runs the distribution at port-provider construction time, and re-runs it when ports are added or removed at runtime.

## Goals

- Omitting `PortDefinition.Fraction` in AXAML is sufficient to get sane edge distribution for any number of ports — no consumer arithmetic.
- Mixed declarations (some ports pinned with explicit fractions, others auto on the same edge) are supported and predictable.
- Runtime additions or removals of auto ports trigger re-distribution on the affected edge with INPC chained through to `Port.Position` / `AbsolutePosition` / `EmissionDirection`.
- No new public types. No public strategy interface in v1.
- No breaking changes to `NodeTemplate` / `NodePortRegistry`'s registration shape beyond the nullable-Fraction ripple.

## Non-goals

- **Public `IPortLayout` strategy interface.** Single built-in algorithm in v1. If a future consumer needs custom math, the interface can be added with the current behavior as the default — no breaking change.
- **`DynamicPortProvider` integration.** DPP creates ports at hit-test points; "layout" doesn't have the same meaning there. Out of scope.
- **Programmatic flip between auto and pinned at runtime.** `Port.IsAutoFraction` is set at construction and immutable after. A future API can be added if needed.
- **Cross-edge port migration.** `Port.Anchor.Edge` remains immutable; only `Fraction` is mutated by layout.
- **Per-port-size offsets, collision avoidance, label-aware spacing.** Pure fraction math. Visual legibility at high port counts is the consumer's responsibility.
- **Batch `LayoutChanging`/`LayoutChanged` events on `FixedPortProvider`.** The existing per-port INPC chain carries the signal. Can be added if a profile shows redraw thrash.

## Locked-in decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | Declarative + runtime re-layout (vs declarative-only or DPP) | Real consumers add/remove ports dynamically; declarative-only forces them to rebuild providers manually. |
| 2 | Opt-in via nullable `Fraction` (vs `PortLayout` attribute or both) | One concept, fully XAML-ergonomic, supports the mixed case naturally. |
| 3 | Distribution math `(i+1)/(N_auto+1)` | De-facto standard in node editors (Unreal BP, n8n). Always interior — never on corners. |
| 4 | Mixed-pin semantic = **Independent** | Pinned ports don't participate in auto math; auto ports use `N_auto`. No declaration-order surprises, no gap-filling algorithm. |
| 5 | Built-in only for v1, no public `IPortLayout` | Pre-1.0 keeps surface minimal. Interface can be added later non-breakingly. |

## Design

### Architecture & data flow

Three layers; one owner of layout math.

```
PortDefinition (XAML)        Fraction: double?   ──┐
   │                                                │ validate + project
   ▼                                                ▼
PortSpec (registry snapshot) Fraction: double?   ──┐
   │                                                │ Node.EnsureMaterialized
   ▼                                                ▼
Port (live model)            IsAutoFraction: bool, Anchor: PortAnchor (internally settable)
   │                                                ▲
   ▼                                                │ SetAnchor on layout pass
FixedPortProvider  ◄── owns the layout algorithm ──┘
```

`NodePortRegistry` is **geometry-agnostic** — it validates and projects `PortDefinition` to `PortSpec` but never computes layout. The auto/pinned bit is preserved end-to-end via `Fraction.HasValue`.

`FixedPortProvider` is the **sole owner** of layout. Layout runs in three places only:

1. **Construction (batch path).** After all ports are buffered into `_ports`, `DistributeAuto` runs once per distinct edge that contains at least one auto port. The set of affected edges is computed in a single pass over `_ports`. Layout completes **before** the `Ports` collection is observable.
2. **`AddPort(port)`.** If `port.IsAutoFraction == true`, `DistributeAuto` runs on `port.Anchor.Edge` **before** `PortAdded` fires — so every subscriber observes a fully-laid-out collection. Pinned add → no layout pass.
3. **`RemovePort(port)`.** If the removed port was auto, `DistributeAuto` runs on the removed edge **before** `PortRemoved` fires. Pinned remove → no layout pass.

### Public surface delta

#### `PortDefinition` — nullable Fraction

```csharp
namespace NodiumGraph.Controls;

public sealed class PortDefinition
{
    public string Name { get; set; } = string.Empty;
    public PortFlow Flow { get; set; } = PortFlow.Input;
    public PortEdge Edge { get; set; } = PortEdge.Left;
    public double? Fraction { get; set; }      // was: double = 0.5
    public string? Label { get; set; }
    public uint? MaxConnections { get; set; }
    public object? DataType { get; set; }
}
```

XAML omitting `Fraction` now produces `null` (auto), not `0.5` (centered-on-edge). This is a pre-1.0 breaking change for any consumer relying on the old default — call it out in the changelog when 1.0 ships; until then, no shim.

#### `PortSpec` — Fraction becomes nullable

```csharp
namespace NodiumGraph;

public readonly record struct PortSpec(
    string Name,
    PortFlow Flow,
    PortEdge Edge,
    double? Fraction,                          // was: double
    string? Label,
    uint? MaxConnections,
    object? DataType);
```

Registry snapshots preserve the auto/pinned bit. Today the only `PortSpec` reader is `Node.EnsureMaterialized` — it branches on `Fraction.HasValue`.

#### `Port` — IsAutoFraction + internal Anchor mutation

```csharp
namespace NodiumGraph.Model;

public class Port : INotifyPropertyChanged
{
    public PortAnchor Anchor { get; private set; }   // was: { get; }
    public bool IsAutoFraction { get; }              // NEW; readonly, set at ctor

    // Existing primary ctor — pinned port. Unchanged signature.
    public Port(Node owner, string name, PortFlow flow, PortAnchor anchor);

    // NEW overload — auto-layout port. Anchor seeded to (edge, 0.5).
    // Provider overwrites before any observer sees the port.
    public Port(Node owner, string name, PortFlow flow, PortEdge edge);

    // NEW internal — sole entry point for layout-driven Anchor mutation.
    // No-op when newAnchor equals current.
    // Throws InvalidOperationException if:
    //   - the port is pinned (IsAutoFraction == false), OR
    //   - newAnchor.Edge != current Anchor.Edge (Edge is immutable; see I-3).
    internal void SetAnchor(PortAnchor newAnchor);

    // ... rest unchanged ...
}
```

Two constructors instead of an `isAuto` bool parameter — intent is visible at the call site, and the pinned-port API stays exactly as it was.

#### `FixedPortProvider` — no signature change, behavior added

Existing constructors and `AddPort`/`RemovePort` signatures unchanged. New behavior:

```csharp
public class FixedPortProvider : IPortProvider
{
    // Ctors: after loading _ports, run DistributeAuto once per affected edge,
    // BEFORE Ports is observable.
    public FixedPortProvider(double hitRadius = DefaultHitRadius);
    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius);

    // AddPort: _ports.Add → DistributeAuto (if auto) → PortAdded
    public void AddPort(Port port);

    // RemovePort: _ports.Remove → Detach → DistributeAuto (if was auto) → PortRemoved
    public bool RemovePort(Port port);
}
```

#### `NodePortRegistry.BuildSnapshot` — null Fraction now valid

Validation gains one branch: `if (d.Fraction is double f && (double.IsNaN(f) || f < 0.0 || f > 1.0)) throw …`. `null` passes. Otherwise unchanged.

#### `Node.EnsureMaterialized` — branch on Fraction.HasValue

```csharp
var ports = specs.Select(s =>
{
    var port = s.Fraction.HasValue
        ? new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction.Value))
        : new Port(this, s.Name, s.Flow, s.Edge);        // auto ctor
    port.Label = s.Label;
    port.MaxConnections = s.MaxConnections;
    port.DataType = s.DataType;
    return port;
}).ToList();

PortProvider = new FixedPortProvider(ports);             // runs initial layout
```

### Layout algorithm

Pure function, deterministic, per-edge.

```csharp
// Private static inside FixedPortProvider.
static void DistributeAuto(IReadOnlyList<Port> ports, PortEdge edge)
{
    int autoCount = 0;
    foreach (var p in ports)
        if (p.Anchor.Edge == edge && p.IsAutoFraction) autoCount++;

    if (autoCount == 0) return;

    int autoIndex = 0;
    foreach (var p in ports)
    {
        if (p.Anchor.Edge != edge || !p.IsAutoFraction) continue;
        double f = (autoIndex + 1.0) / (autoCount + 1.0);
        p.SetAnchor(new PortAnchor(edge, f));
        autoIndex++;
    }
}
```

Properties:

- **Order = insertion order in `_ports`.** Declaration order for the batch ctor path; `AddPort` call order for runtime additions.
- **Idempotent.** Calling twice with the same inputs produces no INPC events (`SetAnchor`'s structural equality short-circuit relies on `PortAnchor` being a `record struct`).
- **Edge-scoped.** Only auto ports on the named edge are touched.
- **Pinned ports ignored for the math.** `N` counts auto ports only.

Worked examples:

| Declaration on Left edge | Auto count | Computed fractions |
|---|---:|---|
| `In1` auto, `In2` auto, `In3` auto | 3 | 0.25, 0.5, 0.75 |
| `In1` auto, `In2` auto | 2 | 0.333, 0.667 |
| `In1` pinned 0.1, `In2` auto, `In3` auto, `In4` pinned 0.9 | 2 | In2 → 0.333, In3 → 0.667 |
| `In1` pinned 0.5, `In2` auto | 1 | In2 → 0.5 (collides with In1 — accepted) |
| Single auto port | 1 | 0.5 |
| No auto ports | 0 | no-op |

Complexity: O(N) per layout pass, N = total ports in the provider. M auto adds one-by-one to the same edge → O(M²) total. Comfortably below any threshold worth optimizing for realistic port counts (<50/edge).

### Runtime invalidation (INPC chain)

```
FixedPortProvider.AddPort(autoPort)
    ├─ _ports.Add(autoPort)
    ├─ DistributeAuto(_ports, autoPort.Anchor.Edge)
    │      └─ for each auto port on edge (insertion order):
    │             port.SetAnchor(new PortAnchor(edge, fraction))
    │                  ├─ if (newAnchor == _anchor) return;          // idempotent
    │                  ├─ _anchor = newAnchor
    │                  ├─ _positionDirty = true
    │                  ├─ _absolutePositionDirty = true
    │                  ├─ OnPropertyChanged(nameof(Anchor))
    │                  ├─ OnPropertyChanged(nameof(Position))
    │                  ├─ OnPropertyChanged(nameof(AbsolutePosition))
    │                  └─ OnPropertyChanged(nameof(EmissionDirection))
    └─ PortAdded?.Invoke(autoPort)                                   // fires after layout
```

`RemovePort` is the mirror: `_ports.Remove` → `port.Detach()` → `DistributeAuto` (if the removed port was auto) → `PortRemoved`.

**What's guaranteed, what isn't:**

- **Guaranteed:** `PortAdded` / `PortRemoved` subscribers see a fully-laid-out `Ports` collection. These are the **provider membership events** — they fire once per add/remove call, after the layout pass completes.
- **Not guaranteed:** Per-port `PropertyChanged` (Anchor / Position / AbsolutePosition / EmissionDirection) is fired *inside* the `DistributeAuto` loop, port-by-port. A handler reacting to one port's INPC during a re-layout will observe a partially-redistributed edge — earlier ports in the loop are already updated, later ports still hold their pre-layout fractions. Treat port-level INPC as a **layout-invalidation signal, not an atomic snapshot**. Subscribers needing a consistent view across the whole edge should re-read all relevant ports after the burst settles (or hook the membership events instead).
- Adding a batch `LayoutChanging` / `LayoutChanged` pair on `FixedPortProvider` would close this gap; explicitly out of scope for v1 (see Non-goals).

What invalidates what:

| Owner change | Triggers cache reset for |
|---|---|
| `Node.Width / Height / Shape` (existing) | Position, AbsolutePosition, EmissionDirection |
| `Node.X / Y` (existing) | AbsolutePosition |
| `Port.Anchor` (new, via `SetAnchor`) | Position, AbsolutePosition, EmissionDirection |

### Canvas connection-cache touchpoint (required)

A consequence the model layer can't solve on its own: today `NodiumGraphCanvas` invalidates cached connection geometry only via the node-level paths (`Node.X / Y / Width / Height / Shape`). See `NodiumGraphCanvas.OnNodePropertyChanged` (current line ~1910) and `InvalidateConnectionGeometryForNode` (current line ~1447). The existing port-level handler at `OnPortPropertyChanged` (current line ~1896) only invalidates **node adornments** when `Port.AbsolutePosition` / `Label` change — it does **not** touch the connection geometry cache.

With auto-layout, an `AddPort` / `RemovePort` of an auto port can move *already-connected* ports while the node is stationary. Existing connections would render against stale cached endpoints.

**Required canvas delta** (part of this spec; not deferred):

In `OnPortPropertyChanged`, extend the `Port.AbsolutePosition` branch to also call `InvalidateConnectionGeometryForNode(port.Owner)` before `InvalidateNodeAdornments`. This piggybacks on the existing per-node cache-invalidation helper — no new infrastructure, single line of net change.

```csharp
else if (e.PropertyName is nameof(Port.AbsolutePosition) or nameof(Port.Label))
{
    if (sender is Port p)
    {
        if (e.PropertyName == nameof(Port.AbsolutePosition))
            InvalidateConnectionGeometryForNode(p.Owner);     // NEW
        InvalidateNodeAdornments(p.Owner);
    }
}
```

`Label` does not affect connection geometry; only `AbsolutePosition` triggers the connection cache drop. After invalidation, the next render rebuilds the affected paths from the moved port's coordinates via the existing connection-render path.

**Required cache-probe test** in `NodiumGraphCanvasTests` (or a sibling file — beware the canvas-suite parallel-flake noted in [[feedback_avalonia_test_flakiness]], use isolated execution):

- `AddPort_AutoPort_InvalidatesConnectionGeometryForConnectedPorts` — set up two unconnected nodes A and B, each with one auto port connected to each other; force both connection geometries into the cache by rendering; add a second auto port to A (which moves A's port via re-layout); assert via the existing `ConnectionGeometryCacheContains` helper that the A↔B connection is **gone from the cache** while an unrelated connection (e.g., between two other nodes C↔D that the test also set up) **remains cached**. This rules out a false pass from a wholesale `InvalidateAllConnectionGeometry` call.

This is the only canvas-side delta required by the spec.

### Invariants

- **I-1.** A `Port` with `IsAutoFraction = true` owned by a `FixedPortProvider` always has its `Anchor.Fraction` equal to what `DistributeAuto` would compute at that moment.
- **I-2.** `Port.IsAutoFraction` is immutable post-construction. No public API flips it.
- **I-3.** `Port.Anchor.Edge` is immutable post-construction. Layout never moves a port across edges; only its `Fraction` changes. **Enforced at the mutation site**: `SetAnchor` throws when `newAnchor.Edge != current.Edge`, so no internal caller can violate this even by mistake.
- **I-4.** `FixedPortProvider` is the sole mutator of `Port.Anchor` for auto ports.
- **I-5.** A pinned `Port.Anchor` is never mutated. **Enforced at the mutation site**: `SetAnchor` throws when called on a port with `IsAutoFraction == false`.

### Edge cases

| # | Scenario | Behavior |
|---|---|---|
| 1 | All ports on an edge are pinned | No layout pass. PortSpec.Fraction.Value used directly. |
| 2 | All ports on an edge are auto | Layout runs; fractions `(i+1)/(N+1)` in insertion order. |
| 3 | Mixed pinned + auto on the same edge | Layout runs over auto only; pinned untouched. |
| 4 | Empty `Ports` on an edge | Layout no-op. |
| 5 | `PortDefinition.Fraction = null` in XAML | Treated as auto. Default. |
| 6 | `PortDefinition.Fraction = 0` or `1` | Pinned to the corner. Allowed (`PortAnchor` validates `[0,1]` inclusive). Does not affect auto math. |
| 7 | `PortDefinition.Fraction` out of `[0,1]` | Throws at `NodePortRegistry.BuildSnapshot`, same as today. |
| 8 | Consumer constructs auto-ctor `Port` and never adds it to a provider | Anchor stays at placeholder `(edge, 0.5)`. Documented quirk; `IsAutoFraction = true` implies "managed by a provider". |
| 9 | `AddPort` during a `PortAdded` handler | Re-entrant; outer layout pass uses the snapshot at its call time. Inner call runs its own pass. Convergent. Not a public guarantee — handlers SHOULD treat the collection as read-only. |
| 10 | Remove the only auto port on an edge | After removal, `autoCount == 0` → `DistributeAuto` no-ops. PortRemoved fires. |
| 11 | `SetAnchor` invoked on a pinned port (library-internal bug) | Throws `InvalidOperationException`. |
| 11b | `SetAnchor` invoked on an auto port with a different `Edge` (library-internal bug) | Throws `InvalidOperationException`. Enforces I-3 at the mutation site, not just in `DistributeAuto`'s call shape. |
| 12 | 50+ auto ports on the same edge | Works mathematically. Visual legibility is the consumer's concern. |
| 13 | Re-layout during INPC handler of an affected port | Convergent; same semantics as #9. |

## Testing

Most model/provider logic is plain CLR — no headless Avalonia required, sidestepping the canvas-suite flake noted in [[feedback_avalonia_test_flakiness]]. The cache-invalidation regression test for the canvas delta is the exception: it uses the existing `AvaloniaFact` style and should run in the isolated execution path the rest of the canvas suite uses.

New test files:

- `tests/NodiumGraph.Tests/FixedPortProviderLayoutTests.cs` — primary suite.
- `tests/NodiumGraph.Tests/PortSetAnchorTests.cs` — unit tests for the new internal mutation path (via `InternalsVisibleTo`).

Extended test files:

- `NodePortRegistryTests` — `null` Fraction passes validation; explicit out-of-range still throws.
- `PortTests` — `IsAutoFraction` property; auto-ctor seeds Anchor at `(edge, 0.5)`.
- `FixedPortProviderTests` — ctor layout pass; AddPort/RemovePort behavior change; layout-runs-before-events.
- Node materialization tests — auto specs round-trip to laid-out ports end-to-end.

Coverage matrix:

| Test | Asserts |
|---|---|
| `Ctor_AllAutoOnOneEdge_Distributes` | `(i+1)/(N+1)` for N = 1..5 |
| `Ctor_MixedPinnedAndAuto_AutoIgnoresPinned` | Pinned 0.1+0.9 with 2 auto → auto at 0.333, 0.667 |
| `Ctor_NoAutoPorts_NoLayoutPass` | Pinned-only: no Anchor mutation, no INPC fired |
| `Ctor_LayoutCompletesBeforePortsExposed` | Reading `Ports` after ctor returns post-layout fractions |
| `AddPort_AutoPort_TriggersEdgeRelayout` | 4th auto on 3-auto edge → 0.2, 0.4, 0.6, 0.8 |
| `AddPort_PinnedPort_SkipsLayout` | Existing auto fractions unchanged |
| `AddPort_LayoutRunsBeforePortAdded` | `PortAdded` subscriber reads `newPort.Position` → post-layout value |
| `RemovePort_AutoPort_TriggersEdgeRelayout` | Remove middle of 3 auto → remaining two → 0.333, 0.667 |
| `RemovePort_PinnedPort_SkipsLayout` | Existing auto fractions on same edge unchanged |
| `RemovePort_LastAutoOnEdge_NoOpLayout` | Remove last auto → no exception, no further INPC |
| `RemovePort_LayoutRunsBeforePortRemoved` | `PortRemoved` subscriber reads remaining ports' Position → post-layout |
| `SetAnchor_OnPinnedPort_Throws` | `InvalidOperationException` |
| `SetAnchor_DifferentEdge_Throws` | `InvalidOperationException` — direct guard test, independent of `DistributeAuto`'s honour-the-edge behavior (covers I-3 against any future internal caller) |
| `SetAnchor_IdempotentNoOp_FiresNoINPC` | Same anchor twice → zero `PropertyChanged` events on the second call |
| `SetAnchor_InvalidatesDependentCaches` | Position / AbsolutePosition / EmissionDirection reflect new Anchor |
| `Anchor_Edge_NeverChangesViaLayout` | Re-layout never alters Edge — only Fraction |
| `PortSpec_FractionNull_ValidPassesRegistry` | `<PortDefinition Edge="Left"/>` survives `BuildSnapshot` |
| `NodeMaterialization_AutoSpec_ProducesLaidOutPorts` | End-to-end: 3 auto-spec ports → fractions 0.25, 0.5, 0.75 |
| `NodeMaterialization_MixedSpec_RespectsPinnedAndDistributesAuto` | End-to-end mixed case |

Test infrastructure notes:

- **Registry isolation.** Tests calling `NodePortRegistry.Register` must `Clear` in fixture/dispose to avoid cross-test pollution. Pattern is already established in `NodePortRegistryTests`.
- **INPC verification.** No formal helper exists; the established pattern (see `PortTests`, `NodeTests`) is `var fired = new List<string>(); port.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);` then assert on the list. Use this inline form for the new tests. Required for the idempotency tests where the assertion is `Assert.Empty(fired)`.

Out of scope for v1 tests:

- Visual rendering of laid-out ports — covered transitively by the existing canvas suite once anchors are correct.
- Performance benchmarks — O(N) per pass with N < 50 is below any threshold worth measuring.

## Documentation updates

These updates ship with the feature (not as a follow-up):

- `docs/userguide/2-how-to/declare-ports-in-axaml.md` — add an "Auto-layout" section after the existing fraction examples. Cover: omit `Fraction` → evenly distributed; `(i+1)/(N+1)` formula; the mixed pinned-and-auto case with the "independent" semantic; declaration order determines on-edge position.
- `docs/userguide/3-reference/model.md` — document `Port.IsAutoFraction` (immutable, set at construction) and the nullable semantics of `PortDefinition.Fraction` / `PortSpec.Fraction` (null = auto). Note that `Port.Anchor` is observable via INPC but only mutates for auto ports under provider control.
- `docs/userguide/2-how-to/persist-graph-state.md` — **add a persistence callout**: serializing only `Port.Anchor.Fraction` loses the auto/pinned intent. A round-tripped port reloads as **pinned at the saved fraction**, which silently breaks runtime re-layout (subsequent `AddPort` / `RemovePort` on the same edge will leave the round-tripped port untouched). Two recommended approaches:
    1. **Persist by spec, not by port.** Store the original `PortDefinition` / `PortSpec` shape (incl. null Fraction); rehydrate through `NodePortRegistry` + `Node.EnsureMaterialized`. Layout re-runs naturally.
    2. **Persist `IsAutoFraction` alongside `Fraction`.** Consumers writing custom port serialization should include the bit explicitly. Provide an example struct in the doc.
- `docs/userguide/2-how-to/custom-port-provider.md` — `FixedPortProvider.AddPort` / `RemovePort` are not virtual, so subclassing isn't the extension model. Add a line for consumers writing a **custom `IPortProvider`** that opts into auto-layout-style behavior: preserve the layout-before-events ordering (run any layout pass before firing `PortAdded` / `PortRemoved`) so subscribers observe a consistent collection.

## Related

- [[2026-05-13-anchor-based-port-positioning-design]] — supplies `PortAnchor`, `Port.Position`-derives-from-Anchor.
- [[2026-05-14-declarative-axaml-ports-design]] — supplies `NodeTemplate`, `NodePortRegistry`, `PortSpec`, `PortDefinition`.
- [[2026-04-13-port-improvements-prioritized]] — original priority list; this is item #2 (Tier S).
