---
title: ConnectionVerdict — richer IConnectionValidator return for drag-time UI feedback
tags: [plan, spec]
status: active
created: 2026-05-19
updated: 2026-05-19
---

# ConnectionVerdict — richer IConnectionValidator return for drag-time UI feedback

Today `IConnectionValidator.CanConnect(Port, Port)` returns a `bool`. During a connection drag, the canvas collapses every rejection reason — self-loop, same-node, wrong-flow, type mismatch — into the same visual cue. The library ships `Port.DataType` and `Port.MaxConnections` as public metadata, but the default validator only reads `DataType`; `MaxConnections` is data with no behavior attached.

The mental model: **a validator returns a verdict, not a verdict-flattened-to-bool**. The verdict's vocabulary matches the granularity of UI feedback the drag-time UI is expected to give — coarse where the UI doesn't care (all generic rejections are `Invalid`), specific where it does (`WrongType` and `AlreadyFull` get distinct cues). The data is rich now; the visual treatment can grow into it in a follow-up.

## Goals

- Replace `IConnectionValidator.CanConnect(Port, Port) → bool` with `Validate(Port source, Port target, IReadOnlyList<Connection> existing) → ConnectionVerdict`.
- Ship a 4-value `ConnectionVerdict { Valid, Invalid, WrongType, AlreadyFull }` enum.
- Extend `DefaultConnectionValidator` so `Port.MaxConnections` is read and emits `AlreadyFull` when either port is at its cap.
- Thread the verdict through `NodiumGraphCanvas` so `_connectionPreviewValid : bool` becomes `_connectionPreviewVerdict : ConnectionVerdict`, exposed for downstream consumers via the existing internal accessor pattern.
- Preserve today's binary visual treatment in `CanvasOverlay` for v1 — the verdict data lands now; per-verdict colors land with the upcoming `HoveredPort` work.

## Non-goals

- **Per-verdict visual styling.** `CanvasOverlay` will continue to do `verdict == Valid ? validPen : invalidPen` for both the target-port highlight and the preview-line. Distinct cues for `WrongType` vs `AlreadyFull` are deferred to the follow-up paired with [[project_portlayout]]-style work on `HoveredPort`.
- **Public `Verdict` struct with a reason string.** The 4-value enum is the public contract; freeform reason text would need a separate channel and is YAGNI at this scale.
- **Connection-count caching on `Port`.** The validator iterates `existingConnections` directly; O(N) per call is comfortable at the project's stated 1000-connection target. A cached count would couple `Port` to `Graph` mutations.
- **`Invalid` sub-categorization.** Self-loop, same-owner, and same-flow all collapse to `Invalid`. Consumers needing the distinction implement their own validator and define their own conventions.
- **Symbol-level `DataType` compatibility** (interface implementation, subclass relation, structural typing). Strict equality preserved. Consumers wanting richer rules supply their own validator.
- **Cycle detection / graph-level checks.** Not a validator concern; would belong to a separate component if ever needed.

## Locked-in decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | Enum vocabulary: `Valid / Invalid / WrongType / AlreadyFull` | UI-feedback granularity, not validator-check granularity. Smallest enum that supports the rich drag-time UI we actually want. |
| 2 | API replacement: rename to `Validate`, return `ConnectionVerdict` | Pre-1.0, no public users — clean break. Name aligns with the interface (`IConnectionValidator.Validate`). |
| 3 | Default validator adds `AlreadyFull` check (either port at `MaxConnections`) | Without this the default never emits one of the four enum values — the rich UI is neutered for consumers who don't roll their own validator. |
| 4 | Validator receives `IReadOnlyList<Connection> existingConnections` as a 3rd param | Smallest surface that lets the default validator count `MaxConnections`. Canvas passes `Graph.Connections` (already in hand at both call sites). |
| 5 | Visuals: data only — binary `verdict == Valid` treatment in `CanvasOverlay` | Verdict data exposed; per-verdict colors deferred to the next item. No palette decisions needed in this PR. |

## Design

### Architecture & data flow

```
DefaultConnectionValidator  ──┐  implements
                              ▼
IConnectionValidator
   ConnectionVerdict Validate(Port src, Port tgt, IReadOnlyList<Connection> existing)
                              ▲  consumes
                              │
NodiumGraphCanvas             │
   _connectionPreviewVerdict: ConnectionVerdict
   ConnectionPreviewVerdict { get; }       (internal, replaces ConnectionPreviewValid)
                              ▲  reads (binary check for v1)
                              │
CanvasOverlay
   target-port highlight ellipse: verdict == Valid ? previewValidPen : cuttingPen (red)
   preview-line drag:             verdict == Valid ? previewValidPen : previewInvalidPen
```

**Lifecycle:**

1. **Mouse-move during connection drag** (`OnPointerMoved`, current line ~907) — canvas resolves the hovered target port. With a non-null target, call `ConnectionValidator?.Validate(source, target, Graph?.Connections ?? Array.Empty<Connection>()) ?? ConnectionVerdict.Valid`. With a null target, set verdict to `ConnectionVerdict.Invalid` directly. Store as `_connectionPreviewVerdict`. `InvalidateVisual()`.

2. **Drop** (`OnPointerReleased`, current line ~1063) — same call, same fallback. Compare `verdict == ConnectionVerdict.Valid` → gate `OnConnectionRequested`. Non-Valid → no connection formed.

3. **Overlay render** — the two existing call sites keep their existing distinct invalid brushes: the target-port highlight ellipse uses `cuttingPen` (red) for non-`Valid`, the preview-line uses the `ConnectionPreviewInvalidBrushKey`-resolved pen. v1 changes only the truth value (`bool` → `verdict == ConnectionVerdict.Valid`), not the brush selection at either site.

**Why the change is breaking in API but small in shape:** Pre-1.0, no public users (per `CLAUDE.md`). The interface method is renamed, the return type changes, and the signature gains a third parameter — but every internal caller (two canvas sites and the test suite) updates trivially in lock-step.

### Public surface delta

#### New type — `ConnectionVerdict`

```csharp
namespace NodiumGraph.Interactions;

/// <summary>
/// Result of a connection-validity check. UI-feedback granularity — distinct
/// values exist where the drag-time UI is expected to give distinct visual cues.
/// Generic rejection reasons (self-loop, same-node, wrong-flow, custom-validator
/// policy) collapse to <see cref="Invalid"/>.
/// </summary>
public enum ConnectionVerdict
{
    /// <summary>The connection is allowed.</summary>
    Valid = 0,

    /// <summary>Rejected for a generic reason — wrong direction, same node,
    /// self-loop, custom policy. UI typically shows the standard "no" cue.</summary>
    Invalid = 1,

    /// <summary>Source and target port <see cref="Model.Port.DataType"/> are not
    /// equal. UI typically shows a type-mismatch cue.</summary>
    WrongType = 2,

    /// <summary>Either the source or target port has reached its
    /// <see cref="Model.Port.MaxConnections"/> and cannot accept another connection.</summary>
    AlreadyFull = 3,
}
```

#### Changed — `IConnectionValidator`

```csharp
namespace NodiumGraph.Interactions;

public interface IConnectionValidator
{
    /// <summary>
    /// Validates whether a connection between two ports is allowed. Called from the
    /// canvas during drag (for live UI feedback) and at drop (for the final gate).
    /// Receives the current connection set so implementations can check per-port
    /// connection counts (e.g. <see cref="Model.Port.MaxConnections"/>) without
    /// reaching back into the graph.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// If <paramref name="source"/>, <paramref name="target"/>, or
    /// <paramref name="existingConnections"/> is null.
    /// </exception>
    ConnectionVerdict Validate(Port source, Port target, IReadOnlyList<Connection> existingConnections);
}
```

Pre-rollout `bool CanConnect(Port, Port)` is removed. No deprecation shim.

#### Changed — `DefaultConnectionValidator`

```csharp
namespace NodiumGraph.Interactions;

public sealed class DefaultConnectionValidator : IConnectionValidator
{
    public static DefaultConnectionValidator Instance { get; } = new();

    public ConnectionVerdict Validate(Port source, Port target,
                                      IReadOnlyList<Connection> existingConnections)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(existingConnections);

        // Generic-reject checks first — cheap, cover the bulk of invalid drags.
        if (ReferenceEquals(source, target)) return ConnectionVerdict.Invalid;
        if (source.Owner == target.Owner)    return ConnectionVerdict.Invalid;
        if (source.Flow == target.Flow)      return ConnectionVerdict.Invalid;

        if (!Equals(source.DataType, target.DataType)) return ConnectionVerdict.WrongType;

        if (IsAtMax(source, existingConnections) ||
            IsAtMax(target, existingConnections))
            return ConnectionVerdict.AlreadyFull;

        return ConnectionVerdict.Valid;
    }

    private static bool IsAtMax(Port port, IReadOnlyList<Connection> existing)
    {
        if (port.MaxConnections is not uint cap) return false;
        if (cap == 0u) return true;                                 // never accepts a connection
        int count = 0;
        for (int i = 0; i < existing.Count; i++)
        {
            var c = existing[i];
            if (ReferenceEquals(c.SourcePort, port) || ReferenceEquals(c.TargetPort, port))
            {
                count++;
                if (count >= cap) return true;
            }
        }
        return false;
    }
}
```

#### Changed — `NodiumGraphCanvas` (no public-API change, internal accessor rename)

```csharp
// Field & internal accessor — rename + retype
- private bool _connectionPreviewValid;
+ private ConnectionVerdict _connectionPreviewVerdict;

- internal bool ConnectionPreviewValid => _connectionPreviewValid;
+ internal ConnectionVerdict ConnectionPreviewVerdict => _connectionPreviewVerdict;

// Reset to "no connection in progress" (current line ~837)
- _connectionPreviewValid = false;
+ _connectionPreviewVerdict = ConnectionVerdict.Invalid;

// OnPointerMoved during connection drag (current line ~907)
- _connectionPreviewValid = _connectionTargetPort != null &&
-     (ConnectionValidator?.CanConnect(_connectionSourcePort, _connectionTargetPort) ?? true);
+ _connectionPreviewVerdict = _connectionTargetPort == null
+     ? ConnectionVerdict.Invalid
+     : (ConnectionValidator?.Validate(_connectionSourcePort,
+                                      _connectionTargetPort,
+                                      Graph?.Connections ?? Array.Empty<Connection>())
+        ?? ConnectionVerdict.Valid);

// OnPointerReleased drop gate (current line ~1063)
- var canConnect = ConnectionValidator?.CanConnect(_connectionSourcePort, targetPort) ?? true;
- if (canConnect)
+ var dropVerdict = ConnectionValidator?.Validate(_connectionSourcePort, targetPort,
+                                                 Graph?.Connections ?? Array.Empty<Connection>())
+                   ?? ConnectionVerdict.Valid;
+ if (dropVerdict == ConnectionVerdict.Valid)
```

#### Changed — `CanvasOverlay` (binary translation, v1)

Two existing call sites, each keeping its distinct invalid brush. Only the truth value changes (`bool` → `verdict == Valid`); brush selection is preserved exactly as today.

```csharp
// Site 1 — target-port highlight ellipse (current line ~75):
- var pen = _canvas.ConnectionPreviewValid ? previewValidPen : cuttingPen; // red for invalid
+ var pen = _canvas.ConnectionPreviewVerdict == ConnectionVerdict.Valid
+     ? previewValidPen
+     : cuttingPen; // red for invalid

// Site 2 — preview-line drag (current line ~86):
- var previewPen = _canvas.ConnectionPreviewValid
-     ? _canvas.ResolvePen(NodiumGraphResources.ConnectionPreviewValidBrushKey, ...)
-     : _canvas.ResolvePen(NodiumGraphResources.ConnectionPreviewInvalidBrushKey, ...);
+ var previewPen = _canvas.ConnectionPreviewVerdict == ConnectionVerdict.Valid
+     ? _canvas.ResolvePen(NodiumGraphResources.ConnectionPreviewValidBrushKey, ...)
+     : _canvas.ResolvePen(NodiumGraphResources.ConnectionPreviewInvalidBrushKey, ...);
```

(Resource keys and `DashStyle` arguments preserved verbatim from the current code at both sites.)

### `DefaultConnectionValidator` semantics

**Check chain, in order:**

| # | Check | Verdict on fail |
|---|---|---|
| 1 | `ReferenceEquals(source, target)` | `Invalid` |
| 2 | `source.Owner == target.Owner` | `Invalid` |
| 3 | `source.Flow == target.Flow` | `Invalid` |
| 4 | `!Equals(source.DataType, target.DataType)` | `WrongType` |
| 5 | `source.MaxConnections` or `target.MaxConnections` cap reached in `existingConnections` | `AlreadyFull` |
| 6 | All checks pass | `Valid` |

**Why this order:**

- Cheap, structural checks first. Self-loop, same-owner, same-flow are reference/enum comparisons. O(1).
- Type check before count check. Type is more fundamental; a wrong-type connection can't be "made full" by checking the count first.
- Count check last. `IsAtMax` is the only O(N) step, scanning `existingConnections`. Running it only when the candidate has otherwise passed minimizes the iteration cost.

**Mapping rationale:**

| Default check failure | Verdict | UI intent |
|---|---|---|
| Self-loop / same owner / same flow | `Invalid` | Standard "no" cue — UI doesn't need to distinguish these. |
| `DataType` mismatch | `WrongType` | Distinct cue — actionable error (wrong wire type). |
| Source or target at `MaxConnections` | `AlreadyFull` | Distinct cue — port is full, suggests rearrangement, not a wrong connection. |

**Edge cases:**

| Case | Behavior |
|---|---|
| `source.MaxConnections` is null but `target.MaxConnections` is set | Only target is checked. |
| Both ports have null `MaxConnections` | Step 5 is a no-op; verdict is `Valid` if 1-4 passed. |
| `MaxConnections == 0` | Cap is immediately reached; returns `AlreadyFull` regardless of `existingConnections`. |
| `existingConnections` is empty | Step 5 only emits `AlreadyFull` if `MaxConnections == 0`. |
| Connection in the list touches neither port | Counter doesn't advance — short-circuits correctly. |
| Connection in the list touches both `source` and `target` | Counted once for each — a parallel connection consumes a slot on both ports. |
| Both `DataType`s are null | `Equals(null, null) == true` → step 4 passes (strict null-matches-null semantic preserved). |

### Canvas integration & invariants

**State machine for `_connectionPreviewVerdict`:**

```
Initial (no drag in progress):     Invalid  (field default; consumers shouldn't read it outside an active drag)
Mouse-down on source port:         Invalid  (drag started, no target yet)
Mouse-move with no target:         Invalid
Mouse-move with target:            Validate(...) → Valid | Invalid | WrongType | AlreadyFull
Drag cancel / pointer release:     Invalid  (reset for next drag)
```

**Invariants:**

- **I-1.** `_connectionPreviewVerdict` is only mutated inside `NodiumGraphCanvas`. No external setter.
- **I-2.** Whenever `_connectionPreviewVerdict` changes during an active drag, `InvalidateVisual()` is called. Overlay reads the verdict during render; the cached pen must match the current verdict.
- **I-3.** Outside an active drag (`_isDrawingConnection == false`), the verdict's value is meaningless. Documented; matches today's `_connectionPreviewValid` contract.
- **I-4.** Drop and preview use the same `Validate` call shape. A `Valid` at drag-time means a `Valid` at drop-time, assuming the graph hasn't mutated between mouse-move and release. No further race-condition expectation.
- **I-5.** A null `ConnectionValidator` is equivalent to a permissive validator: every call's verdict is `ConnectionVerdict.Valid`. Preserves today's `?? true` semantics.

**`ConnectionValidatorProperty`'s default:** stays `DefaultConnectionValidator.Instance`. Consumers who never touched `ConnectionValidator` get the new `Validate` path automatically.

**Graph null-safety:** Both canvas call sites coalesce `Graph?.Connections` to `Array.Empty<Connection>()`. Custom validators get a non-null list. `DefaultConnectionValidator`'s `IsAtMax` runs its loop against the (possibly empty) list — but the `cap == 0` short-circuit still fires before the loop, so a port with `MaxConnections == 0` still returns `AlreadyFull` even against an empty list. Only ports with `MaxConnections > 0` or null `MaxConnections` fall through to `Valid` when the list is empty.

### Test plan

New test file:

- `tests/NodiumGraph.Tests/Interactions/ConnectionVerdictTests.cs` — enum sanity (value layout, no surprises).

Extended test files:

- `tests/NodiumGraph.Tests/Interactions/DefaultConnectionValidatorTests.cs` — rewrite for the new method shape. Add `MaxConnections` coverage.
- `tests/NodiumGraph.Tests/Controls/NodiumGraphCanvasConnectionDefaultsTests.cs` — extend for verdict surface; spy-validator tests.

**Coverage matrix:**

| Test | Asserts |
|---|---|
| `Validate_self_loop_returns_Invalid` | `source == target` reference-equal → `Invalid` |
| `Validate_same_owner_returns_Invalid` | Two ports on same node → `Invalid` |
| `Validate_same_flow_returns_Invalid` | Input+Input or Output+Output → `Invalid` |
| `Validate_different_datatype_returns_WrongType` | Mismatched `DataType` → `WrongType` |
| `Validate_both_datatypes_null_passes_type_check` | `null == null` → step 4 passes, proceeds to step 5 |
| `Validate_source_at_MaxConnections_returns_AlreadyFull` | `source.MaxConnections = 1`, list has 1 source-touching connection → `AlreadyFull` |
| `Validate_target_at_MaxConnections_returns_AlreadyFull` | Same but for target |
| `Validate_MaxConnections_zero_returns_AlreadyFull_immediately` | Cap = 0, empty list → `AlreadyFull` |
| `Validate_null_MaxConnections_does_not_check_cap` | Both caps null, list has N connections → `Valid` |
| `Validate_connection_not_touching_port_does_not_count` | Cap = 1, one unrelated connection → `Valid` |
| `Validate_returns_Valid_when_all_checks_pass` | Happy path |
| `Validate_throws_on_null_source` | `ArgumentNullException` |
| `Validate_throws_on_null_target` | `ArgumentNullException` |
| `Validate_throws_on_null_existingConnections` | `ArgumentNullException` |
| `Validate_type_check_runs_before_count_check` | Mismatched type + at-MaxConnections → `WrongType` (not `AlreadyFull`); order is observable |
| Canvas: `OnPointerMoved_with_null_validator_sets_verdict_Valid` | Null validator → `Valid` on any target hover |
| Canvas: `OnPointerMoved_with_no_target_sets_verdict_Invalid` | Hover empty space → `Invalid` |
| Canvas: `OnPointerMoved_passes_Graph_Connections_to_validator` | Spy validator captures the third argument; assert it's `Graph.Connections` |
| Canvas: `OnPointerReleased_drop_gated_by_Valid` | Non-Valid verdict → no `OnConnectionRequested` invocation |
| Canvas: `OnPointerReleased_drop_with_null_Graph_passes_empty_list` | Graph = null → validator sees `Array.Empty<Connection>()` |

**Test infrastructure notes:**

- `DefaultConnectionValidator` tests are plain CLR — no headless Avalonia required. Sidesteps the canvas-suite parallel-flake noted in [[feedback_avalonia_test_flakiness]].
- Canvas tests follow the existing `AvaloniaFact` pattern; run in isolation if the suite flakes.
- A small `RecordingValidator : IConnectionValidator` test helper captures the args of each `Validate` call. Worth adding once and reusing across canvas tests.

## Documentation updates

These ship with the feature (not as a follow-up). Every consumer-facing reference to `bool CanConnect(Port, Port)` must be updated — leaving stale examples after a breaking API rename is the kind of small thing that costs trust.

**Per-page edits:**

- `docs/userguide/2-how-to/custom-validator.md` — rewrite end-to-end: new method shape (`Validate(source, target, existingConnections) → ConnectionVerdict`), the four verdict values and when to use each, the `DefaultConnectionValidator.Instance.Validate(...)` composition pattern (replacing the existing `.CanConnect(...)` pattern at line 47 and elsewhere). Worked example: a typed-port validator returning `WrongType` for incompatible types and `AlreadyFull` when an externally-tracked count is reached.
- `docs/userguide/3-reference/handlers.md` — update the `IConnectionValidator` entry: new signature, return type, when each verdict value applies. Also update the cross-reference at line ~53 ("Only fires after `IConnectionValidator.CanConnect` returned `true`") to reference `Validate` returning `Valid`.
- `docs/userguide/3-reference/strategies.md` — update the interface declaration (line ~57) and the example custom-validator block (lines ~84-86) to the new signature.
- `docs/userguide/3-reference/canvas-control.md` — update the `ConnectionValidator` property description (line ~52): "accept/reject predicate" is no longer accurate; reword to "validator that returns a `ConnectionVerdict` for live feedback during a connection drag".
- `docs/userguide/3-reference/model.md` — `Port.MaxConnections` entry: clarify that `DefaultConnectionValidator` now reads this and emits `AlreadyFull` when either port hits the cap (was metadata-only before).
- `docs/userguide/1-tutorial/getting-started.md` — the `DefaultConnectionValidator.Instance` rejection-list section (around line 262) gains a new bullet: "either port at `MaxConnections` (returns `AlreadyFull`)". The pre-existing four bullets stay; one is added.
- `docs/userguide/2-how-to/style-ports.md` — if any reference to `ConnectionPreviewValid` exists, update to `ConnectionPreviewVerdict` (or remove if the page was describing consumer-facing surface only; this internal probe was never public).
- `CLAUDE.md` (project root) — `IConnectionValidator` line in the "Interaction Handlers" section: change `CanConnect(source, target)` to `Validate(source, target, existingConnections) → ConnectionVerdict`.
- `AGENTS.md` (project root) — mirror the `CLAUDE.md` change.

**Migration sweep — required before merging:** `grep -r "CanConnect" docs/ AGENTS.md CLAUDE.md` must return zero hits from user-facing files. Pre-existing planning docs under `docs/plans/` and `docs/superpowers/` describing historical decisions may keep their `CanConnect` references — they are point-in-time records, not consumer guidance. The same goes for `docs/nodiumgraph-design.md` if its sections describe what the API used to be. Use judgment: anything a reader would land on while learning the current library must be updated; anything that is a historical artifact may stay.

## Related

- Builds on [[2026-05-19-portlayout-design]] (commit `4e12baf`) — the recent PortLayout rollout established the pattern for pre-1.0 breaking API changes and the test/doc rhythm followed here.
- Successor item from [[2026-04-13-port-improvements-prioritized]] — this is Tier S #3 (validation-state visuals during drag), data layer.
- Next roadmap item after this: `HoveredPort` + `IPortHoverHandler`. Per-verdict visual styling lands there.
