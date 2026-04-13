---
title: Port Data Types + Type-Aware Default Validator
tags: [spec, plan]
status: active
created: 2026-04-13
updated: 2026-04-13
---

# Port Data Types + Type-Aware Default Validator

First slice of the port-improvements bundle (see [[2026-04-13-port-improvements-prioritized]], item #1). Adds opaque type metadata to `Port` and ships a default `IConnectionValidator` that enforces both `PortFlow` direction and `DataType` compatibility.

## Scope

- Add `object? DataType` to `Port` as opaque metadata.
- Ship `DefaultConnectionValidator` as the canvas's default `IConnectionValidator`.
- Delete the sample's custom validator (default now covers it) and demonstrate type-based rejection in the sample.

## Design

### 1. `Port.DataType`

Mutable property, matches the existing `Label` / `Style` / `MaxConnections` pattern:

```csharp
private object? _dataType;
public object? DataType
{
    get => _dataType;
    set => SetField(ref _dataType, value);
}
```

- **Why `object?`:** opaque token, AOT-clean, no reflection. Consumers pick whatever representation fits (string, enum, `Type`, record).
- **Why mutable:** matches precedent and lets consumers reassign via object initializers. The library does not react to changes (no cascading connection removal) — report-don't-decide.

### 2. `DefaultConnectionValidator`

New file: `src/NodiumGraph/Interactions/DefaultConnectionValidator.cs`.

```csharp
public sealed class DefaultConnectionValidator : IConnectionValidator
{
    public static DefaultConnectionValidator Instance { get; } = new();

    public bool CanConnect(Port source, Port target)
    {
        if (ReferenceEquals(source, target)) return false;
        if (source.Owner == target.Owner) return false;
        if (source.Flow == target.Flow) return false;

        return Equals(source.DataType, target.DataType);
    }
}
```

Rules (in order):

1. **Self:** a port cannot connect to itself.
2. **Same owner:** no intra-node connections (universal in node editors; every consumer would write this).
3. **Flow:** `source.Flow != target.Flow` — enforces Out→In. The sample already carries this line; the library should own it.
4. **DataType (strict null):** `Equals(source.DataType, target.DataType)`. Both null → accept. One null + one typed → reject. Matching typed → accept. Mismatched → reject.

Equality uses `object.Equals`, which handles `string`, `enum`, `System.Type`, and value/record types correctly without reflection.

**Strict null** is deliberate: wildcard-null would silently defeat the feature during incremental adoption — one forgotten `DataType` assignment and the graph reverts to "anything connects to anything" with no signal.

### 3. Canvas wiring

`NodiumGraphCanvas.ConnectionValidatorProperty` default value becomes `DefaultConnectionValidator.Instance` (instead of `null`). The existing `?? true` fallbacks at lines 652 and 819 remain as an escape hatch: setting `ConnectionValidator = null` explicitly disables all validation (useful for tests and free-form authoring).

### 4. Sample cleanup

`samples/NodiumGraph.Sample/MainWindow.axaml.cs`:

- Delete the custom `SimpleConnectionValidator` class and its wiring (`CanConnect` starting at line 167).
- Assign `DataType` to a couple of demo ports to show type-based rejection in action — e.g. mark `inputOut` / `transformIn` as `"number"` and `filterIn` as `"string"` so the user can feel the rejection during drag.

## Tests

New file: `tests/NodiumGraph.Tests/DefaultConnectionValidatorTests.cs`.

| Case | Expected |
|---|---|
| Same port (source == target) | reject |
| Two ports on same node, opposite flow | reject |
| Two nodes, same flow (Out→Out) | reject |
| Two nodes, same flow (In→In) | reject |
| Opposite flow, both `DataType == null` | accept |
| Opposite flow, matching string `DataType` | accept |
| Opposite flow, matching enum `DataType` | accept |
| Opposite flow, matching `Type` `DataType` | accept |
| Opposite flow, mismatched string `DataType` | reject |
| Opposite flow, one null one typed | reject |

Plus one canvas-level test: instantiate `NodiumGraphCanvas` without setting `ConnectionValidator`, attempt a connection between mismatched-type ports, assert rejection. Confirms the default wiring.

## Out of Scope

- **Validation-state visuals (#3):** separate slice.
- **Coercion / type hierarchies** (e.g. Int→Float): consumers subclass `DefaultConnectionValidator` or supply their own.
- **`PortAnchor` / auto-layout (#7, #2):** separate slices.

## Files touched

- `src/NodiumGraph/Model/Port.cs` — add `DataType` property
- `src/NodiumGraph/Interactions/DefaultConnectionValidator.cs` — new
- `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — default property value
- `samples/NodiumGraph.Sample/MainWindow.axaml.cs` — drop custom validator, add demo `DataType`s
- `tests/NodiumGraph.Tests/DefaultConnectionValidatorTests.cs` — new
