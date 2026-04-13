# Port Data Types Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Add opaque `DataType` metadata to `Port` and ship a `DefaultConnectionValidator` (self/same-owner/Flow/DataType rules) wired as the canvas default, replacing the sample's custom validator.

**Architecture:** Pure additive: one property on `Port`, one new sealed validator class, one `StyledProperty` default change on `NodiumGraphCanvas`. Sample swaps its inline validator for the default and demonstrates `DataType` rejection.

**Tech Stack:** .NET 10, Avalonia 12, xUnit v3, Avalonia headless.

**Design doc:** [[2026-04-13-port-data-types-design]]

---

## Task 1: Default validator — failing tests

**Files:**
- Create: `tests/NodiumGraph.Tests/Interactions/DefaultConnectionValidatorTests.cs`

**Step 1: Write the failing test file**

Cases (one `[Fact]` per row):

| Name | Arrange | Expected |
|---|---|---|
| `Rejects_SamePort` | one port, call with (p, p) | false |
| `Rejects_SameOwner` | two ports on same node, opposite Flow | false |
| `Rejects_SameFlow_Output` | two nodes, both Output | false |
| `Rejects_SameFlow_Input` | two nodes, both Input | false |
| `Accepts_OppositeFlow_BothNullDataType` | two nodes, Out + In, `DataType` both null | true |
| `Accepts_OppositeFlow_MatchingString` | `DataType = "number"` on both | true |
| `Accepts_OppositeFlow_MatchingEnum` | custom enum, same value | true |
| `Accepts_OppositeFlow_MatchingType` | `DataType = typeof(int)` on both | true |
| `Rejects_OppositeFlow_MismatchedString` | `"number"` vs `"string"` | false |
| `Rejects_OppositeFlow_OneNullOneTyped` | null vs `"number"` | false |

Use `DefaultConnectionValidator.Instance.CanConnect(source, target)`. Construct `Node` + `Port` directly — no canvas.

Note: `Port.DataType` does not exist yet; tests will also drive its addition.

**Step 2: Run and verify failure**

```
dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter DefaultConnectionValidatorTests
```
Expected: compile errors (`DefaultConnectionValidator`, `Port.DataType` missing).

**Step 3: Commit (red)**

```bash
git add tests/NodiumGraph.Tests/Interactions/DefaultConnectionValidatorTests.cs
git commit -m "test: add failing DefaultConnectionValidator tests"
```

---

## Task 2: Add `Port.DataType`

**Files:**
- Modify: `src/NodiumGraph/Model/Port.cs`

**Step 1: Add backing field and property**

Next to `_maxConnections`:

```csharp
private object? _dataType;
```

Next to `MaxConnections`:

```csharp
/// <summary>
/// Opaque type token consumed by <see cref="IConnectionValidator"/>.
/// The library never inspects this value beyond equality comparison in the default validator.
/// </summary>
public object? DataType
{
    get => _dataType;
    set => SetField(ref _dataType, value);
}
```

**Step 2: Build**

```
dotnet build src/NodiumGraph/NodiumGraph.csproj
```
Expected: success.

**Step 3: Commit**

```bash
git add src/NodiumGraph/Model/Port.cs
git commit -m "feat: add Port.DataType opaque type metadata"
```

---

## Task 3: Implement `DefaultConnectionValidator`

**Files:**
- Create: `src/NodiumGraph/Interactions/DefaultConnectionValidator.cs`

**Step 1: Write the class**

```csharp
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Default validator: rejects self-connections, same-owner connections, same-flow pairs,
/// and mismatched <see cref="Port.DataType"/> values. Null DataType only matches null (strict).
/// </summary>
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

**Step 2: Run the Task 1 tests**

```
dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter DefaultConnectionValidatorTests
```
Expected: all 10 pass.

**Step 3: Commit (green)**

```bash
git add src/NodiumGraph/Interactions/DefaultConnectionValidator.cs
git commit -m "feat: add DefaultConnectionValidator with Flow and DataType rules"
```

---

## Task 4: Wire default into `NodiumGraphCanvas`

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (around line 180)

**Step 1: Change the StyledProperty default**

Replace:

```csharp
public static readonly StyledProperty<IConnectionValidator?> ConnectionValidatorProperty =
    AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionValidator?>(nameof(ConnectionValidator));
```

With:

```csharp
public static readonly StyledProperty<IConnectionValidator?> ConnectionValidatorProperty =
    AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionValidator?>(
        nameof(ConnectionValidator),
        defaultValue: DefaultConnectionValidator.Instance);
```

Leave the existing `?? true` call sites at lines ~652 and ~819 untouched — they remain the escape hatch when a consumer sets `ConnectionValidator = null`.

**Step 2: Build and run full test suite**

```
dotnet test
```
Expected: all tests pass. Existing tests that rely on no-validator behavior must still pass because they either don't drag connections or construct compatible ports.

If any pre-existing test fails because the default now rejects a connection its setup assumed would succeed (e.g. same-owner ports, same-flow ports), fix the test setup to use valid port pairs — do NOT weaken the default.

**Step 3: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "feat: default NodiumGraphCanvas.ConnectionValidator to DefaultConnectionValidator"
```

---

## Task 5: Canvas-level integration test

**Files:**
- Create or modify: `tests/NodiumGraph.Tests/Controls/NodiumGraphCanvasConnectionDefaultsTests.cs` (create if not already present; if a similar file exists, add the test there)

**Step 1: Write the test**

```csharp
[AvaloniaFact]
public void Default_validator_rejects_mismatched_datatypes()
{
    var canvas = new NodiumGraphCanvas();
    // No ConnectionValidator assigned — should fall through to DefaultConnectionValidator.Instance

    var graph = new Graph();
    var a = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var b = new Node { X = 200, Y = 0, Width = 100, Height = 50 };
    var outPort = new Port(a, "out", PortFlow.Output, new Point(100, 25)) { DataType = "number" };
    var inPort = new Port(b, "in", PortFlow.Input, new Point(0, 25)) { DataType = "string" };
    a.PortProvider = new FixedPortProvider(new[] { outPort });
    b.PortProvider = new FixedPortProvider(new[] { inPort });
    graph.AddNode(a);
    graph.AddNode(b);
    canvas.Graph = graph;

    Assert.False(canvas.ConnectionValidator!.CanConnect(outPort, inPort));
}

[AvaloniaFact]
public void Default_validator_accepts_matching_datatypes()
{
    // identical setup but DataType = "number" on both
    // Assert.True(canvas.ConnectionValidator!.CanConnect(outPort, inPort));
}
```

Adapt API calls (`FixedPortProvider` constructor, `Graph.AddNode`) to whatever the current tests use — look at an existing canvas test for the pattern.

**Step 2: Run**

```
dotnet test --filter NodiumGraphCanvasConnectionDefaultsTests
```
Expected: both pass.

**Step 3: Commit**

```bash
git add tests/NodiumGraph.Tests/Controls/NodiumGraphCanvasConnectionDefaultsTests.cs
git commit -m "test: default validator wired into NodiumGraphCanvas"
```

---

## Task 6: Sample cleanup

**Files:**
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml.cs`

**Step 1: Delete the custom validator**

1. Remove the `SimpleConnectionValidator` (or equivalent) class at the bottom of the file (starts around line 165 — the `public bool CanConnect` method lives there).
2. Remove the canvas wiring line that assigns it: grep for `ConnectionValidator =` in this file and delete that assignment.

**Step 2: Add demo `DataType`s**

On a few ports that currently wire together in the sample graph, add `DataType` assignments via object initializer so the user can feel both accept and reject:

```csharp
var inputOut = new Port(inputNode, "out", PortFlow.Output, new Point(120, 30))
    { Label = "out", DataType = "number" };

var transformIn = new Port(transformNode, "in", PortFlow.Input, new Point(0, 30))
    { Label = "in", DataType = "number" };

var filterIn = new Port(filterNode, "in", PortFlow.Input, new Point(0, 30))
    { Label = "in", DataType = "string" };
```

Pick at least one pair that currently exists as a pre-seeded connection. If that pair becomes type-incompatible, either align their `DataType`s or remove the pre-seeded connection — do NOT loosen the validator.

**Step 3: Run the sample**

```
dotnet run --project samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj
```
Expected:
- Sample launches.
- Pre-seeded connections render without errors.
- Dragging from `inputOut` ("number") to `filterIn` ("string") shows rejection feedback.
- Dragging from `inputOut` ("number") to `transformIn` ("number") succeeds.

**Step 4: Commit**

```bash
git add samples/NodiumGraph.Sample/MainWindow.axaml.cs
git commit -m "sample: drop custom validator, demo Port.DataType rejection"
```

---

## Task 7: Final verification

**Step 1: Full suite**

```
dotnet test
```
Expected: all tests pass (should be ≥ 395 + the 10 new validator tests + 2 canvas tests = 407+).

**Step 2: Build release**

```
dotnet build -c Release
```
Expected: no warnings introduced, no errors.

**Step 3: Skim diff for dead code**

```
git log --oneline main..HEAD
git diff main --stat
```
Expected: six commits (Tasks 1–6), ~5 files touched in `src`, `tests`, `samples`.

No final commit — Task 7 is verification only.

---

## Out of Scope (do not expand this plan)

- Validation-state visuals (#3)
- `HoveredPort` state (#5)
- Required/optional markers (#9)
- Coercion rules or type hierarchies
- Anchor/layout work (#7, #2)
