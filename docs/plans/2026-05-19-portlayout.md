---
title: PortLayout — implementation plan
tags: [plan]
status: active
created: 2026-05-19
updated: 2026-05-19
---

# PortLayout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the design at [[2026-05-19-portlayout-design]] (commit `4e12baf`) — declarative ports auto-distribute their fractions when `Fraction` is omitted, and re-distribute on runtime add/remove.

**Architecture:** Six tasks, bottom-up. Make `PortDefinition.Fraction` / `PortSpec.Fraction` nullable end-to-end, add `Port.IsAutoFraction` + an internal `SetAnchor` (guarded), give `FixedPortProvider` an internal `DistributeAuto` that runs in the ctor and on auto add/remove with layout-before-events ordering, wire `Node.EnsureMaterialized` to use the new auto-port ctor, add a canvas-side cache-invalidation delta on `Port.AbsolutePosition`, then refresh four user-guide pages.

**Tech Stack:** .NET 10, Avalonia 12, xUnit v3, `Avalonia.Headless.XUnit` for canvas tests. `InternalsVisibleTo("NodiumGraph.Tests")` is already configured in `src/NodiumGraph/NodiumGraph.csproj` — tests can call `internal SetAnchor` and `internal DistributeAuto` directly.

---

## Pre-flight

- Spec at `docs/plans/2026-05-19-portlayout-design.md` is the source of truth. If a step here disagrees with the spec, the spec wins — flag it.
- Run `dotnet build` and `dotnet test` once before starting Task 1 to confirm the baseline is green.
- Verify tooling: `dotnet --version` should report 10.x; Avalonia 12 packages restore on first build.

---

## Task 1: Nullable Fraction through PortDefinition → PortSpec → Registry (with placeholder Node wiring)

**Goal:** Propagate `double?` through the declarative chain so `<ng:PortDefinition Edge="Left"/>` (no Fraction) compiles, validates, and round-trips through the registry. Leave `Node.EnsureMaterialized` with a `?? 0.5` placeholder for now — Task 4 will replace it.

**Files:**
- Modify: `src/NodiumGraph/Controls/PortDefinition.cs`
- Modify: `src/NodiumGraph/PortSpec.cs`
- Modify: `src/NodiumGraph/NodePortRegistry.cs` (validation branch in `BuildSnapshot`)
- Modify: `src/NodiumGraph/Model/Node.cs` (temporary `?? 0.5` in `EnsureMaterialized`)
- Modify (existing tests): `tests/NodiumGraph.Tests/NodePortRegistryTests.cs` (add null-Fraction tests; adjust any `spec.Fraction` reads to handle nullable)

**Acceptance Criteria:**
- [ ] `PortDefinition.Fraction` is `double?` with no default (defaults to `null`).
- [ ] `PortSpec.Fraction` is `double?`.
- [ ] `NodePortRegistry.BuildSnapshot` accepts `null` Fraction; out-of-range non-null still throws.
- [ ] Existing test suite still passes (modulo nullable-assertion adjustments).
- [ ] New test `Register_PortDefinitionWithNullFraction_PassesValidation` passes.
- [ ] New test `Register_PortDefinitionWithOutOfRangeFraction_StillThrows` passes.

**Verify:** `dotnet test --filter "FullyQualifiedName~NodePortRegistryTests"` → all green.

**Steps:**

- [ ] **Step 1: Write the failing test for null Fraction**

Add to `tests/NodiumGraph.Tests/NodePortRegistryTests.cs` — find the existing `Clear`-using test pattern in that file and follow it. Add **two** new tests at the end of the class:

```csharp
[Fact]
public void Register_PortDefinitionWithNullFraction_PassesValidation()
{
    NodePortRegistry.Clear();
    try
    {
        var def = new PortDefinition
        {
            Name = "In",
            Flow = PortFlow.Input,
            Edge = PortEdge.Left,
            Fraction = null,
        };
        NodePortRegistry.Register(typeof(NullFractionTestNode), new[] { def });
        Assert.True(NodePortRegistry.TryGet(typeof(NullFractionTestNode), out var specs));
        var spec = Assert.Single(specs);
        Assert.Null(spec.Fraction);
    }
    finally
    {
        NodePortRegistry.Clear();
    }
}

[Fact]
public void Register_PortDefinitionWithOutOfRangeFraction_StillThrows()
{
    NodePortRegistry.Clear();
    try
    {
        var def = new PortDefinition
        {
            Name = "In",
            Flow = PortFlow.Input,
            Edge = PortEdge.Left,
            Fraction = 1.5,
        };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NodePortRegistry.Register(typeof(NullFractionTestNode), new[] { def }));
    }
    finally
    {
        NodePortRegistry.Clear();
    }
}

private sealed class NullFractionTestNode : Node { }
```

- [ ] **Step 2: Run tests to confirm they fail to compile**

Run: `dotnet build`
Expected: compile error in the test class because `PortDefinition.Fraction = null` is not assignable to `double`.

- [ ] **Step 3: Make `PortDefinition.Fraction` nullable**

Replace the whole class body of `src/NodiumGraph/Controls/PortDefinition.cs`:

```csharp
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// XAML-side construction recipe for a single port. A list of <see cref="PortDefinition"/>
/// appears under <c>&lt;ng:NodeTemplate.Ports&gt;</c>; <c>NodePortRegistry</c> projects each
/// instance into a <see cref="PortSpec"/> at registration time.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Fraction"/> is nullable. <c>null</c> declares intent to auto-layout — the
/// owning <c>FixedPortProvider</c> will distribute auto ports evenly along their edge.
/// A non-null value pins the port at that fraction; the value must lie in <c>[0, 1]</c>.
/// </para>
/// </remarks>
public sealed class PortDefinition
{
    public string Name { get; set; } = string.Empty;
    public PortFlow Flow { get; set; } = PortFlow.Input;
    public PortEdge Edge { get; set; } = PortEdge.Left;
    public double? Fraction { get; set; }

    public string? Label { get; set; }
    public uint? MaxConnections { get; set; }
    public object? DataType { get; set; }
}
```

- [ ] **Step 4: Make `PortSpec.Fraction` nullable**

Replace the record body in `src/NodiumGraph/PortSpec.cs`:

```csharp
using NodiumGraph.Model;

namespace NodiumGraph;

/// <summary>
/// Immutable snapshot of a single port declaration in <c>NodePortRegistry</c>.
/// Returned from <c>NodePortRegistry.TryGet</c> and consumed by <see cref="Model.Node"/>'s
/// lazy materializer.
/// </summary>
/// <remarks>
/// <see cref="Fraction"/> is nullable. <c>null</c> means "auto-layout" — the consumer
/// (<see cref="Model.Node.EnsureMaterialized"/>) constructs the port via the auto ctor,
/// and <c>FixedPortProvider</c> resolves the actual fraction at provider construction.
/// </remarks>
public readonly record struct PortSpec(
    string Name,
    PortFlow Flow,
    PortEdge Edge,
    double? Fraction,
    string? Label,
    uint? MaxConnections,
    object? DataType);
```

- [ ] **Step 5: Update `NodePortRegistry.BuildSnapshot` validation**

In `src/NodiumGraph/NodePortRegistry.cs`, find the `BuildSnapshot` method (it begins `private static IReadOnlyList<PortSpec> BuildSnapshot(...)`). Replace the Fraction-validation line:

```csharp
if (double.IsNaN(d.Fraction) || d.Fraction < 0.0 || d.Fraction > 1.0)
    throw new ArgumentOutOfRangeException(nameof(definitions), $"Fraction {d.Fraction} for '{d.Name}' is not in [0,1].");
```

with:

```csharp
if (d.Fraction is double f && (double.IsNaN(f) || f < 0.0 || f > 1.0))
    throw new ArgumentOutOfRangeException(nameof(definitions), $"Fraction {d.Fraction} for '{d.Name}' is not in [0,1].");
```

The `specs.Add(new PortSpec(...))` line below it needs no change — both types now match (`double? → double?`).

- [ ] **Step 6: Patch `Node.EnsureMaterialized` to compile against nullable Fraction (placeholder)**

In `src/NodiumGraph/Model/Node.cs`, find `EnsureMaterialized` (around line 189). The current line:

```csharp
var ports = specs.Select(s => new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction))
```

becomes (temporary; Task 4 replaces this with proper branching):

```csharp
var ports = specs.Select(s => new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction ?? 0.5))
```

Leave the rest of the lambda body unchanged.

- [ ] **Step 7: Update existing tests that assume non-nullable Fraction**

Search the test project for direct reads of `spec.Fraction` that won't compile under `double?`:

Run: `dotnet build` and address compile errors one by one. Expected hits:
- Anywhere a test asserts `Assert.Equal(0.5, spec.Fraction)` → change to `Assert.Equal(0.5, spec.Fraction)` (works — `Equal<double?>(double, double?)` overload), or `Assert.Equal(0.5, spec.Fraction!.Value)` if the asserter complains.
- Anywhere a test constructs a `PortSpec(... 0.5 ...)` positionally → still works (implicit `double → double?` conversion).
- Anywhere a test reads `def.Fraction` expecting `double` → change to `def.Fraction ?? <expected default>` if the test was probing the old default of `0.5` (which now becomes `null`).

If the existing default-Fraction test in `PortDefinition` test coverage exists and asserts `0.5` as default, **change the assertion to `null`** — the default genuinely changed.

- [ ] **Step 8: Run the full test suite to verify**

Run: `dotnet test`
Expected: all green. If the assertion-default change in Step 7 misses a test, fix and re-run.

- [ ] **Step 9: Run the two new tests in isolation**

Run: `dotnet test --filter "FullyQualifiedName~Register_PortDefinitionWithNullFraction_PassesValidation|FullyQualifiedName~Register_PortDefinitionWithOutOfRangeFraction_StillThrows"`
Expected: 2 passed.

- [ ] **Step 10: Commit**

```bash
git add src/NodiumGraph/Controls/PortDefinition.cs src/NodiumGraph/PortSpec.cs src/NodiumGraph/NodePortRegistry.cs src/NodiumGraph/Model/Node.cs tests/NodiumGraph.Tests/NodePortRegistryTests.cs
git commit -m "feat: nullable PortDefinition.Fraction; PortSpec carries auto-bit

Omitting Fraction in PortDefinition / PortSpec now declares intent to
auto-layout (null sentinel). Registry validation accepts null; non-null
still validates [0,1]. Node.EnsureMaterialized keeps a temporary ?? 0.5
fallback that Task 4 replaces with proper auto-ctor branching.

Part of the PortLayout rollout — see docs/plans/2026-05-19-portlayout-design.md."
```

---

## Task 2: Port — IsAutoFraction, auto-ctor, internal SetAnchor with edge + pinned guards

**Goal:** Add `Port.IsAutoFraction` (immutable, set at ctor), a new `Port(Node, string, PortFlow, PortEdge)` overload for auto ports, and an `internal SetAnchor(PortAnchor)` that mutates the now-private-set `Anchor`, invalidates dependent caches, fires INPC, and throws on pinned-port or different-edge calls.

**Files:**
- Modify: `src/NodiumGraph/Model/Port.cs`
- Create: `tests/NodiumGraph.Tests/PortSetAnchorTests.cs`
- Modify (existing tests): `tests/NodiumGraph.Tests/PortTests.cs` — add `IsAutoFraction` defaults sanity if not covered by the new file.

**Acceptance Criteria:**
- [ ] `Port.IsAutoFraction { get; }` exists and is `false` for the existing 4-param ctor, `true` for the new edge-only ctor.
- [ ] New ctor `Port(Node owner, string name, PortFlow flow, PortEdge edge)` seeds `Anchor` to `new PortAnchor(edge, 0.5)`.
- [ ] `Anchor` setter is `private set` (was readonly auto-property `{ get; }`).
- [ ] `internal void SetAnchor(PortAnchor newAnchor)` exists with the three guards: pinned-port throws `InvalidOperationException`, different-edge throws `InvalidOperationException`, equal anchor short-circuits before any INPC fires.
- [ ] `SetAnchor` mutation invalidates `_positionDirty` / `_absolutePositionDirty` and fires INPC for `Anchor`, `Position`, `AbsolutePosition`, `EmissionDirection`.
- [ ] All tests in the new `PortSetAnchorTests` file pass.
- [ ] Full test suite still passes.

**Verify:** `dotnet test --filter "FullyQualifiedName~PortSetAnchorTests"` → 7 passed, then `dotnet test` → all green.

**Steps:**

- [ ] **Step 1: Create the failing test file**

Create `tests/NodiumGraph.Tests/PortSetAnchorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortSetAnchorTests
{
    private static Node MakeNode(double width = 100, double height = 200)
    {
        var node = new Node();
        // Width/Height have internal setters; use reflection-free workaround if
        // the project exposes a test seam, else mutate via a small TestNode helper.
        // For now: rely on the public default of measured-width/height being
        // available via the existing test helpers — if Node lacks a public way
        // to set size in tests, use the existing pattern from PortTests.cs.
        return node;
    }

    [Fact]
    public void AutoCtor_SetsIsAutoFractionTrue_AndAnchorAtEdgeHalf()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        Assert.True(port.IsAutoFraction);
        Assert.Equal(PortEdge.Left, port.Anchor.Edge);
        Assert.Equal(0.5, port.Anchor.Fraction);
    }

    [Fact]
    public void PinnedCtor_SetsIsAutoFractionFalse()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.3));
        Assert.False(port.IsAutoFraction);
        Assert.Equal(0.3, port.Anchor.Fraction);
    }

    [Fact]
    public void SetAnchor_OnPinnedPort_Throws()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.3));
        Assert.Throws<InvalidOperationException>(() =>
            port.SetAnchor(new PortAnchor(PortEdge.Left, 0.7)));
    }

    [Fact]
    public void SetAnchor_DifferentEdge_Throws()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        Assert.Throws<InvalidOperationException>(() =>
            port.SetAnchor(new PortAnchor(PortEdge.Right, 0.5)));
    }

    [Fact]
    public void SetAnchor_SameAnchor_FiresNoINPC()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        var fired = new List<string>();
        port.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        port.SetAnchor(new PortAnchor(PortEdge.Left, 0.5));   // same as ctor placeholder
        Assert.Empty(fired);
    }

    [Fact]
    public void SetAnchor_NewFraction_FiresINPCForDerivedProperties()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        var fired = new List<string>();
        port.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        port.SetAnchor(new PortAnchor(PortEdge.Left, 0.75));
        Assert.Contains(nameof(Port.Anchor), fired);
        Assert.Contains(nameof(Port.Position), fired);
        Assert.Contains(nameof(Port.AbsolutePosition), fired);
        Assert.Contains(nameof(Port.EmissionDirection), fired);
    }

    [Fact]
    public void SetAnchor_NewFraction_InvalidatesPositionCache()
    {
        var node = MakeNode();
        var port = new Port(node, "In", PortFlow.Input, PortEdge.Left);
        var before = port.Position;
        port.SetAnchor(new PortAnchor(PortEdge.Left, 0.9));
        var after = port.Position;
        Assert.NotEqual(before, after);
    }
}
```

If `MakeNode` cannot set Width/Height because they're `internal set`, follow the established pattern from `tests/NodiumGraph.Tests/PortTests.cs` — that file successfully exercises Position-via-resize and provides a working pattern. Mirror its setup.

- [ ] **Step 2: Run tests to confirm they fail to compile**

Run: `dotnet build`
Expected: compile errors — `IsAutoFraction` and `SetAnchor` don't exist on `Port`, and the `Port(Node, string, PortFlow, PortEdge)` ctor overload doesn't exist.

- [ ] **Step 3: Modify `Port.cs` — add `IsAutoFraction`, auto-ctor, and `SetAnchor`**

In `src/NodiumGraph/Model/Port.cs`:

(a) Change the `Anchor` declaration from `{ get; }` to `{ get; private set; }`:

```csharp
public PortAnchor Anchor { get; private set; }
```

(b) Add `IsAutoFraction` immediately after `Anchor`:

```csharp
/// <summary>
/// True when this port's <see cref="Anchor"/> Fraction is managed by its owning
/// <see cref="FixedPortProvider"/> (distributed evenly along the edge). False when
/// the Fraction was pinned at construction. Immutable post-construction.
/// </summary>
public bool IsAutoFraction { get; }
```

(c) Replace the existing primary ctor body to set `IsAutoFraction = false` explicitly:

```csharp
public Port(Node owner, string name, PortFlow flow, PortAnchor anchor)
{
    Owner  = owner ?? throw new ArgumentNullException(nameof(owner));
    Name   = name  ?? throw new ArgumentNullException(nameof(name));
    Flow   = flow;
    Anchor = anchor;
    IsAutoFraction = false;
    Owner.PropertyChanged += OnOwnerPropertyChanged;
}
```

(d) Add the new auto-port ctor immediately after the existing one:

```csharp
/// <summary>
/// Constructs an auto-layout port. The owning <see cref="FixedPortProvider"/>
/// will overwrite <see cref="Anchor"/>'s Fraction at provider construction (or on
/// runtime add/remove) by calling <see cref="SetAnchor"/>. Until a provider runs
/// layout, the port's Fraction is a placeholder of <c>0.5</c>.
/// </summary>
public Port(Node owner, string name, PortFlow flow, PortEdge edge)
{
    Owner  = owner ?? throw new ArgumentNullException(nameof(owner));
    Name   = name  ?? throw new ArgumentNullException(nameof(name));
    Flow   = flow;
    Anchor = new PortAnchor(edge, 0.5);
    IsAutoFraction = true;
    Owner.PropertyChanged += OnOwnerPropertyChanged;
}
```

(e) Add `SetAnchor` near the end of the class, before `event PropertyChanged`:

```csharp
/// <summary>
/// Sole entry point for layout-driven Anchor mutation. Throws if the port is
/// pinned (<see cref="IsAutoFraction"/> == false) or if <paramref name="newAnchor"/>
/// targets a different Edge — Anchor.Edge is immutable post-construction.
/// No-op when <paramref name="newAnchor"/> equals the current Anchor.
/// </summary>
internal void SetAnchor(PortAnchor newAnchor)
{
    if (!IsAutoFraction)
        throw new InvalidOperationException(
            $"Port '{Name}' is pinned; SetAnchor is reserved for auto-layout ports.");
    if (newAnchor.Edge != Anchor.Edge)
        throw new InvalidOperationException(
            $"Port '{Name}'.Anchor.Edge is immutable; SetAnchor must preserve the edge " +
            $"(current: {Anchor.Edge}, requested: {newAnchor.Edge}).");
    if (newAnchor == Anchor) return;

    Anchor = newAnchor;
    _positionDirty = true;
    _absolutePositionDirty = true;
    OnPropertyChanged(nameof(Anchor));
    OnPropertyChanged(nameof(Position));
    OnPropertyChanged(nameof(AbsolutePosition));
    OnPropertyChanged(nameof(EmissionDirection));
}
```

- [ ] **Step 4: Run the new test file to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~PortSetAnchorTests"`
Expected: 7 passed.

- [ ] **Step 5: Run the full suite to verify no regressions**

Run: `dotnet test`
Expected: all green. Existing `PortTests` continue to pass because the existing ctor's behavior is unchanged (just made `IsAutoFraction = false` explicit).

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/Port.cs tests/NodiumGraph.Tests/PortSetAnchorTests.cs
git commit -m "feat: Port.IsAutoFraction, auto-ctor, internal SetAnchor

Adds an auto-port construction path Port(Node, string, PortFlow, PortEdge)
that seeds the Anchor to (edge, 0.5) and marks IsAutoFraction=true. The
internal SetAnchor mutation point enforces I-3 (edge immutability) and
I-5 (pinned ports never mutate), and chains INPC through Anchor →
Position / AbsolutePosition / EmissionDirection.

Part of the PortLayout rollout — see docs/plans/2026-05-19-portlayout-design.md."
```

---

## Task 3: FixedPortProvider — DistributeAuto, ctor batch layout, layout-before-events ordering

**Goal:** Add a private static `DistributeAuto(IReadOnlyList<Port>, PortEdge)` to `FixedPortProvider`. Run it for each affected edge at the end of the IEnumerable ctor (before any external observer can see `Ports`). Re-run it on auto-port add/remove **before** firing `PortAdded`/`PortRemoved` so subscribers see a fully-laid-out collection.

**Files:**
- Modify: `src/NodiumGraph/Model/FixedPortProvider.cs`
- Create: `tests/NodiumGraph.Tests/FixedPortProviderLayoutTests.cs`

**Acceptance Criteria:**
- [ ] `DistributeAuto(IReadOnlyList<Port>, PortEdge)` is a private static helper inside `FixedPortProvider`. For each auto port on the edge (in `_ports` order), it sets `Anchor` to `new PortAnchor(edge, (i + 1.0) / (autoCount + 1.0))` via `SetAnchor`.
- [ ] The IEnumerable ctor identifies distinct edges that contain at least one auto port and calls `DistributeAuto` once per edge — after `_ports` is fully loaded.
- [ ] The IEnumerable ctor does NOT fire `PortAdded` for initial ports (matches existing behavior).
- [ ] `AddPort`: order is `_ports.Add(port)` → if `port.IsAutoFraction` then `DistributeAuto(_ports, port.Anchor.Edge)` → `PortAdded?.Invoke(port)`.
- [ ] `RemovePort`: order is `_ports.Remove(port)` → `port.Detach()` → if `wasAuto` then `DistributeAuto(_ports, removedEdge)` → `PortRemoved?.Invoke(port)`.
- [ ] All 13 new tests in `FixedPortProviderLayoutTests` pass.
- [ ] Existing `FixedPortProviderTests` continue to pass (no behavior change for pinned-only providers).

**Verify:** `dotnet test --filter "FullyQualifiedName~FixedPortProviderLayoutTests"` → 13 passed, then `dotnet test` → all green.

**Steps:**

- [ ] **Step 1: Create the failing test file**

Create `tests/NodiumGraph.Tests/FixedPortProviderLayoutTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class FixedPortProviderLayoutTests
{
    private static Node MakeNode() => new Node();

    private static Port[] AutoPortsOnLeft(Node node, int count)
    {
        var arr = new Port[count];
        for (int i = 0; i < count; i++)
            arr[i] = new Port(node, $"In{i + 1}", PortFlow.Input, PortEdge.Left);
        return arr;
    }

    [Fact]
    public void Ctor_ThreeAutoOnLeft_Distributes()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        _ = new FixedPortProvider(ports);
        Assert.Equal(0.25, ports[0].Anchor.Fraction);
        Assert.Equal(0.50, ports[1].Anchor.Fraction);
        Assert.Equal(0.75, ports[2].Anchor.Fraction);
    }

    [Fact]
    public void Ctor_FiveAutoOnLeft_Distributes()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 5);
        _ = new FixedPortProvider(ports);
        for (int i = 0; i < 5; i++)
            Assert.Equal((i + 1.0) / 6.0, ports[i].Anchor.Fraction, 9);
    }

    [Fact]
    public void Ctor_SingleAutoOnLeft_AtHalf()
    {
        var node = MakeNode();
        var port = new Port(node, "Only", PortFlow.Input, PortEdge.Left);
        _ = new FixedPortProvider(new[] { port });
        Assert.Equal(0.5, port.Anchor.Fraction);
    }

    [Fact]
    public void Ctor_PinnedAndAutoOnSameEdge_AutoIgnoresPinned()
    {
        var node = MakeNode();
        var pinnedHi = new Port(node, "Hi", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.1));
        var auto1 = new Port(node, "A1", PortFlow.Input, PortEdge.Left);
        var auto2 = new Port(node, "A2", PortFlow.Input, PortEdge.Left);
        var pinnedLo = new Port(node, "Lo", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.9));
        _ = new FixedPortProvider(new[] { pinnedHi, auto1, auto2, pinnedLo });
        Assert.Equal(0.1, pinnedHi.Anchor.Fraction);
        Assert.Equal(0.9, pinnedLo.Anchor.Fraction);
        Assert.Equal(1.0 / 3.0, auto1.Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, auto2.Anchor.Fraction, 9);
    }

    [Fact]
    public void Ctor_AllPinned_FiresNoPortLevelINPC()
    {
        var node = MakeNode();
        var p1 = new Port(node, "P1", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.2));
        var p2 = new Port(node, "P2", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.7));
        var fired = new List<string>();
        p1.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        p2.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
        _ = new FixedPortProvider(new[] { p1, p2 });
        Assert.Empty(fired);
    }

    [Fact]
    public void Ctor_DoesNotFirePortAddedForInitialPorts()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 2);
        var provider = new FixedPortProvider(ports);
        var added = new List<Port>();
        provider.PortAdded += p => added.Add(p);
        // Provider is already constructed; subscribing now must not retroactively fire.
        Assert.Empty(added);
    }

    [Fact]
    public void AddPort_AutoOnExistingEdge_TriggersEdgeRelayout()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        var provider = new FixedPortProvider(ports);
        var p4 = new Port(node, "In4", PortFlow.Input, PortEdge.Left);
        provider.AddPort(p4);
        Assert.Equal(0.2, ports[0].Anchor.Fraction, 9);
        Assert.Equal(0.4, ports[1].Anchor.Fraction, 9);
        Assert.Equal(0.6, ports[2].Anchor.Fraction, 9);
        Assert.Equal(0.8, p4.Anchor.Fraction, 9);
    }

    [Fact]
    public void AddPort_PinnedDoesNotRelayoutAuto()
    {
        var node = MakeNode();
        var auto1 = new Port(node, "A1", PortFlow.Input, PortEdge.Left);
        var auto2 = new Port(node, "A2", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { auto1, auto2 });
        var pinned = new Port(node, "P", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.5));
        provider.AddPort(pinned);
        Assert.Equal(1.0 / 3.0, auto1.Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, auto2.Anchor.Fraction, 9);
    }

    [Fact]
    public void AddPort_LayoutRunsBeforePortAddedFires()
    {
        var node = MakeNode();
        var existing = new Port(node, "E", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { existing });
        double observedExistingFraction = -1;
        provider.PortAdded += _ => observedExistingFraction = existing.Anchor.Fraction;
        var added = new Port(node, "A", PortFlow.Input, PortEdge.Left);
        provider.AddPort(added);
        Assert.Equal(1.0 / 3.0, observedExistingFraction, 9);
    }

    [Fact]
    public void RemovePort_AutoMid_TriggersEdgeRelayout()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        var provider = new FixedPortProvider(ports);
        provider.RemovePort(ports[1]);
        Assert.Equal(1.0 / 3.0, ports[0].Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, ports[2].Anchor.Fraction, 9);
    }

    [Fact]
    public void RemovePort_LastAutoOnEdge_NoOp()
    {
        var node = MakeNode();
        var port = new Port(node, "Only", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { port });
        var removed = provider.RemovePort(port);
        Assert.True(removed);
    }

    [Fact]
    public void RemovePort_PinnedDoesNotRelayoutAuto()
    {
        var node = MakeNode();
        var pinned = new Port(node, "P", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.5));
        var auto1 = new Port(node, "A1", PortFlow.Input, PortEdge.Left);
        var auto2 = new Port(node, "A2", PortFlow.Input, PortEdge.Left);
        var provider = new FixedPortProvider(new[] { pinned, auto1, auto2 });
        provider.RemovePort(pinned);
        Assert.Equal(1.0 / 3.0, auto1.Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, auto2.Anchor.Fraction, 9);
    }

    [Fact]
    public void RemovePort_LayoutRunsBeforePortRemovedFires()
    {
        var node = MakeNode();
        var ports = AutoPortsOnLeft(node, 3);
        var provider = new FixedPortProvider(ports);
        double observedFirstFraction = -1;
        provider.PortRemoved += _ => observedFirstFraction = ports[0].Anchor.Fraction;
        provider.RemovePort(ports[1]);
        Assert.Equal(1.0 / 3.0, observedFirstFraction, 9);
    }

    [Fact]
    public void AutoPorts_PreserveEdgeAcrossLayout()
    {
        var node = MakeNode();
        var leftA = new Port(node, "LA", PortFlow.Input, PortEdge.Left);
        var rightA = new Port(node, "RA", PortFlow.Output, PortEdge.Right);
        var provider = new FixedPortProvider(new[] { leftA, rightA });
        provider.AddPort(new Port(node, "LB", PortFlow.Input, PortEdge.Left));
        Assert.Equal(PortEdge.Left, leftA.Anchor.Edge);
        Assert.Equal(PortEdge.Right, rightA.Anchor.Edge);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~FixedPortProviderLayoutTests"`
Expected: 13 failures. The IEnumerable ctor doesn't currently distribute auto ports — they all stay at the placeholder 0.5 — so distribution assertions fail. Layout-before-events tests fail because PortAdded fires before any (still missing) layout pass.

- [ ] **Step 3: Modify `FixedPortProvider.cs` — add DistributeAuto + ctor batch + ordering**

In `src/NodiumGraph/Model/FixedPortProvider.cs`:

(a) Update the IEnumerable ctor body. Replace:

```csharp
public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius) : this(hitRadius)
{
    ArgumentNullException.ThrowIfNull(ports);
    foreach (var port in ports)
    {
        ArgumentNullException.ThrowIfNull(port, nameof(ports));
        _ports.Add(port);
    }
}
```

with:

```csharp
public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius) : this(hitRadius)
{
    ArgumentNullException.ThrowIfNull(ports);
    foreach (var port in ports)
    {
        ArgumentNullException.ThrowIfNull(port, nameof(ports));
        _ports.Add(port);
    }

    // Initial layout pass: distribute auto ports on each edge that has any.
    // Runs after all ports are buffered so per-edge auto count is correct.
    // Does NOT fire PortAdded — these are initial members, not runtime additions.
    var edgesWithAuto = new HashSet<PortEdge>();
    foreach (var p in _ports)
        if (p.IsAutoFraction) edgesWithAuto.Add(p.Anchor.Edge);
    foreach (var edge in edgesWithAuto)
        DistributeAuto(_ports, edge);
}
```

(b) Update `AddPort` to run layout before firing the event. Replace:

```csharp
public void AddPort(Port port)
{
    ArgumentNullException.ThrowIfNull(port);
    _ports.Add(port);
    PortAdded?.Invoke(port);
}
```

with:

```csharp
public void AddPort(Port port)
{
    ArgumentNullException.ThrowIfNull(port);
    _ports.Add(port);
    if (port.IsAutoFraction)
        DistributeAuto(_ports, port.Anchor.Edge);
    PortAdded?.Invoke(port);
}
```

(c) Update `RemovePort` similarly. Replace:

```csharp
public bool RemovePort(Port port)
{
    ArgumentNullException.ThrowIfNull(port);
    if (!_ports.Remove(port)) return false;
    port.Detach();
    PortRemoved?.Invoke(port);
    return true;
}
```

with:

```csharp
public bool RemovePort(Port port)
{
    ArgumentNullException.ThrowIfNull(port);
    if (!_ports.Remove(port)) return false;
    var wasAuto = port.IsAutoFraction;
    var removedEdge = port.Anchor.Edge;
    port.Detach();
    if (wasAuto)
        DistributeAuto(_ports, removedEdge);
    PortRemoved?.Invoke(port);
    return true;
}
```

(d) Add the `DistributeAuto` helper at the end of the class, before the closing brace:

```csharp
/// <summary>
/// For every auto port on <paramref name="edge"/>, set its Anchor to
/// <c>(edge, (i + 1) / (N_auto + 1))</c> where <c>i</c> is the port's index among auto
/// ports on that edge (insertion order in <paramref name="ports"/>) and <c>N_auto</c> is
/// the total count of auto ports on the edge. Pinned ports are ignored.
/// Idempotent: <see cref="Port.SetAnchor"/>'s structural-equality short-circuit means a
/// no-op pass fires zero INPC events.
/// </summary>
private static void DistributeAuto(IReadOnlyList<Port> ports, PortEdge edge)
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

- [ ] **Step 4: Run the new test file to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~FixedPortProviderLayoutTests"`
Expected: 13 passed.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: all green. Existing `FixedPortProviderTests` should pass unchanged — they construct ports through the pinned ctor (no auto ports), so the new layout pass is a no-op for them.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/FixedPortProvider.cs tests/NodiumGraph.Tests/FixedPortProviderLayoutTests.cs
git commit -m "feat: FixedPortProvider auto-layout via DistributeAuto

Provider now distributes auto ports along their edge using the
(i+1)/(N_auto+1) formula. Initial layout runs once per affected edge
at the end of the IEnumerable ctor; runtime add/remove re-runs the
edge-scoped pass before firing PortAdded / PortRemoved so subscribers
see a fully-laid-out collection. Pinned ports are ignored — the
mixed-pin semantic is Independent (auto uses N_auto only).

Part of the PortLayout rollout — see docs/plans/2026-05-19-portlayout-design.md."
```

---

## Task 4: Node.EnsureMaterialized — branch on Fraction.HasValue for the auto Port ctor

**Goal:** Replace the temporary `?? 0.5` placeholder from Task 1 with the proper branch: pinned spec → pinned `Port` ctor, auto spec → new edge-only `Port` ctor. End-to-end: declaring auto ports in AXAML now produces evenly-laid-out `Node.Ports` on first read.

**Files:**
- Modify: `src/NodiumGraph/Model/Node.cs` (`EnsureMaterialized` body)
- Modify (existing tests): `tests/NodiumGraph.Tests/NodeRegistryMaterializationTests.cs` — add three new end-to-end tests.

**Acceptance Criteria:**
- [ ] `EnsureMaterialized` selects between the two `Port` ctors based on `s.Fraction.HasValue`. No `?? 0.5` remains.
- [ ] New test `EnsureMaterialized_ThreeAutoSpecs_ProduceEvenlyDistributedPorts` passes.
- [ ] New test `EnsureMaterialized_MixedSpec_RespectsPinnedAndDistributesAuto` passes.
- [ ] New test `EnsureMaterialized_AutoSpecPort_HasIsAutoFractionTrue` passes.
- [ ] Full test suite stays green.

**Verify:** `dotnet test --filter "FullyQualifiedName~NodeRegistryMaterializationTests"` → all green (existing + 3 new).

**Steps:**

- [ ] **Step 1: Add the three failing tests**

In `tests/NodiumGraph.Tests/NodeRegistryMaterializationTests.cs`, follow the existing test pattern (Clear → Register → instantiate Node → assert on `node.Ports`). Add three tests:

```csharp
[Fact]
public void EnsureMaterialized_ThreeAutoSpecs_ProduceEvenlyDistributedPorts()
{
    NodePortRegistry.Clear();
    try
    {
        var defs = new[]
        {
            new PortDefinition { Name = "In1", Flow = PortFlow.Input, Edge = PortEdge.Left },
            new PortDefinition { Name = "In2", Flow = PortFlow.Input, Edge = PortEdge.Left },
            new PortDefinition { Name = "In3", Flow = PortFlow.Input, Edge = PortEdge.Left },
        };
        NodePortRegistry.Register(typeof(AutoNodeAlpha), defs);
        var node = new AutoNodeAlpha();
        var ports = node.Ports.ToList();
        Assert.Equal(3, ports.Count);
        Assert.Equal(0.25, ports[0].Anchor.Fraction);
        Assert.Equal(0.50, ports[1].Anchor.Fraction);
        Assert.Equal(0.75, ports[2].Anchor.Fraction);
    }
    finally { NodePortRegistry.Clear(); }
}

[Fact]
public void EnsureMaterialized_MixedSpec_RespectsPinnedAndDistributesAuto()
{
    NodePortRegistry.Clear();
    try
    {
        var defs = new[]
        {
            new PortDefinition { Name = "Top",    Flow = PortFlow.Input,  Edge = PortEdge.Left, Fraction = 0.1 },
            new PortDefinition { Name = "Mid1",   Flow = PortFlow.Input,  Edge = PortEdge.Left },
            new PortDefinition { Name = "Mid2",   Flow = PortFlow.Input,  Edge = PortEdge.Left },
            new PortDefinition { Name = "Bottom", Flow = PortFlow.Input,  Edge = PortEdge.Left, Fraction = 0.9 },
        };
        NodePortRegistry.Register(typeof(AutoNodeBeta), defs);
        var node = new AutoNodeBeta();
        var byName = node.Ports.ToDictionary(p => p.Name);
        Assert.Equal(0.1, byName["Top"].Anchor.Fraction);
        Assert.Equal(0.9, byName["Bottom"].Anchor.Fraction);
        Assert.Equal(1.0 / 3.0, byName["Mid1"].Anchor.Fraction, 9);
        Assert.Equal(2.0 / 3.0, byName["Mid2"].Anchor.Fraction, 9);
    }
    finally { NodePortRegistry.Clear(); }
}

[Fact]
public void EnsureMaterialized_AutoSpecPort_HasIsAutoFractionTrue()
{
    NodePortRegistry.Clear();
    try
    {
        var defs = new[]
        {
            new PortDefinition { Name = "Auto",   Flow = PortFlow.Input,  Edge = PortEdge.Left },
            new PortDefinition { Name = "Pinned", Flow = PortFlow.Input,  Edge = PortEdge.Right, Fraction = 0.5 },
        };
        NodePortRegistry.Register(typeof(AutoNodeGamma), defs);
        var node = new AutoNodeGamma();
        var byName = node.Ports.ToDictionary(p => p.Name);
        Assert.True(byName["Auto"].IsAutoFraction);
        Assert.False(byName["Pinned"].IsAutoFraction);
    }
    finally { NodePortRegistry.Clear(); }
}

private sealed class AutoNodeAlpha : Node { }
private sealed class AutoNodeBeta  : Node { }
private sealed class AutoNodeGamma : Node { }
```

If the existing test class uses different naming for its inner test-Node subtypes, follow the existing convention. If a `using System.Linq;` import is missing, add it.

- [ ] **Step 2: Run to confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~EnsureMaterialized_ThreeAutoSpecs"`
Expected: failure — the placeholder `?? 0.5` from Task 1 stacks all three ports at 0.5.

- [ ] **Step 3: Modify `EnsureMaterialized`**

In `src/NodiumGraph/Model/Node.cs`, find the `EnsureMaterialized` method and replace its `var ports = ...` block:

```csharp
var ports = specs.Select(s => new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction ?? 0.5))
{
    Label = s.Label,
    MaxConnections = s.MaxConnections,
    DataType = s.DataType,
}).ToList();
```

with:

```csharp
var ports = specs.Select(s =>
{
    var port = s.Fraction.HasValue
        ? new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction.Value))
        : new Port(this, s.Name, s.Flow, s.Edge);
    port.Label = s.Label;
    port.MaxConnections = s.MaxConnections;
    port.DataType = s.DataType;
    return port;
}).ToList();
```

Leave the `PortProvider = new FixedPortProvider(ports);` line below it unchanged — that's where the initial layout pass from Task 3 runs.

- [ ] **Step 4: Run the new tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~EnsureMaterialized_"`
Expected: 3 new tests pass; existing materialization tests still green.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/Node.cs tests/NodiumGraph.Tests/NodeRegistryMaterializationTests.cs
git commit -m "feat: Node.EnsureMaterialized branches on Fraction.HasValue for auto port ctor

Pinned spec (Fraction has value) routes through the existing
Port(Node, string, PortFlow, PortAnchor) ctor. Auto spec (Fraction
null) routes through the new Port(Node, string, PortFlow, PortEdge)
ctor; FixedPortProvider's initial layout pass distributes the
fractions. Removes the temporary ?? 0.5 placeholder from Task 1.

Part of the PortLayout rollout — see docs/plans/2026-05-19-portlayout-design.md."
```

---

## Task 5: Canvas — invalidate connection geometry on Port.AbsolutePosition

**Goal:** When a port's `AbsolutePosition` changes (e.g., because `DistributeAuto` re-set its Anchor while the node was stationary), the canvas must drop the cached connection geometry for that port's owner — otherwise existing connections render against stale endpoints.

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (`OnPortPropertyChanged`)
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasPortLayoutCacheTests.cs`

**Acceptance Criteria:**
- [ ] `OnPortPropertyChanged` calls `InvalidateConnectionGeometryForNode(p.Owner)` when `e.PropertyName == nameof(Port.AbsolutePosition)`. The existing `InvalidateNodeAdornments(p.Owner)` call still runs (for both AbsolutePosition AND Label).
- [ ] `Label` change does NOT trigger connection cache invalidation.
- [ ] New `AvaloniaFact` test `AutoPort_AddOnSameEdge_InvalidatesAffectedConnectionOnly_LeavesUnrelatedCached` passes — confirms the touched connection's cache entry is dropped AND an unrelated cached connection remains. This rules out a false pass from a wholesale `InvalidateAllConnectionGeometry`.
- [ ] No regression in `NodiumGraphCanvasConnectionCacheTests`.

**Verify:** `dotnet test --filter "FullyQualifiedName~NodiumGraphCanvasPortLayoutCacheTests"` → 1 passed (run in isolation per [[feedback_avalonia_test_flakiness]]); then `dotnet test --filter "FullyQualifiedName~NodiumGraphCanvas"` → green.

**Steps:**

- [ ] **Step 1: Create the failing canvas test**

Create `tests/NodiumGraph.Tests/NodiumGraphCanvasPortLayoutCacheTests.cs`:

```csharp
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

/// <summary>
/// Pins the cache-invalidation delta required by PortLayout: when an auto port is
/// added to an edge that already has connected auto ports, the existing ports move
/// (via DistributeAuto) while their owning node is stationary. The canvas must drop
/// the cached connection geometry for that node — but only for that node, not all
/// connections.
/// </summary>
public class NodiumGraphCanvasPortLayoutCacheTests
{
    private const int CanvasWidth = 800;
    private const int CanvasHeight = 600;

    [AvaloniaFact]
    public void AutoPort_AddOnSameEdge_InvalidatesAffectedConnectionOnly_LeavesUnrelatedCached()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();

        // Pair A↔B: auto ports on the moving side.
        var nodeA = new Node { X = 100, Y = 100 };
        var nodeB = new Node { X = 400, Y = 100 };
        var aPortOut = new Port(nodeA, "out", PortFlow.Output, PortEdge.Right);
        var bPortIn = new Port(nodeB, "in", PortFlow.Input, PortEdge.Left);
        nodeA.PortProvider = new FixedPortProvider(new[] { aPortOut });
        nodeB.PortProvider = new FixedPortProvider(new[] { bPortIn });

        // Pair C↔D: pinned ports, unrelated; cache must survive.
        var nodeC = new Node { X = 100, Y = 400 };
        var nodeD = new Node { X = 400, Y = 400 };
        var cPortOut = new Port(nodeC, "out", PortFlow.Output, new PortAnchor(PortEdge.Right, 0.5));
        var dPortIn = new Port(nodeD, "in", PortFlow.Input, new PortAnchor(PortEdge.Left, 0.5));
        nodeC.PortProvider = new FixedPortProvider(new[] { cPortOut });
        nodeD.PortProvider = new FixedPortProvider(new[] { dPortIn });

        graph.AddNode(nodeA); graph.AddNode(nodeB);
        graph.AddNode(nodeC); graph.AddNode(nodeD);

        var ab = new Connection(aPortOut, bPortIn);
        var cd = new Connection(cPortOut, dPortIn);
        graph.AddConnection(ab);
        graph.AddConnection(cd);

        canvas.Graph = graph;
        canvas.Measure(new Size(CanvasWidth, CanvasHeight));
        canvas.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));

        using (var bmp = new RenderTargetBitmap(new PixelSize(CanvasWidth, CanvasHeight)))
        using (var ctx = bmp.CreateDrawingContext())
            canvas.Render(ctx);

        Assert.True(canvas.ConnectionGeometryCacheContains(ab.Id),
            "AB must be cached after first render.");
        Assert.True(canvas.ConnectionGeometryCacheContains(cd.Id),
            "CD must be cached after first render.");

        // Trigger DistributeAuto on nodeA's edge: add a second auto port to the same edge.
        // This moves aPortOut and invalidates its AbsolutePosition.
        var aPortOut2 = new Port(nodeA, "out2", PortFlow.Output, PortEdge.Right);
        ((FixedPortProvider)nodeA.PortProvider!).AddPort(aPortOut2);

        Assert.False(canvas.ConnectionGeometryCacheContains(ab.Id),
            "AB cache must drop — its source port's AbsolutePosition changed.");
        Assert.True(canvas.ConnectionGeometryCacheContains(cd.Id),
            "CD cache must remain — unrelated to the moved node.");
    }
}
```

- [ ] **Step 2: Run to confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~NodiumGraphCanvasPortLayoutCacheTests"`
Expected: failure. The current `OnPortPropertyChanged` only invalidates node adornments, not connection geometry — AB's cache survives the move and the assertion `Assert.False(... ab.Id)` fails.

- [ ] **Step 3: Modify `OnPortPropertyChanged`**

In `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`, find `OnPortPropertyChanged` (around line 1876). Locate the `else if` for `Port.AbsolutePosition or nameof(Port.Label)`:

```csharp
else if (e.PropertyName is nameof(Port.AbsolutePosition) or nameof(Port.Label))
{
    if (sender is Port p)
        InvalidateNodeAdornments(p.Owner);
}
```

Replace with:

```csharp
else if (e.PropertyName is nameof(Port.AbsolutePosition) or nameof(Port.Label))
{
    if (sender is Port p)
    {
        if (e.PropertyName == nameof(Port.AbsolutePosition))
            InvalidateConnectionGeometryForNode(p.Owner);
        InvalidateNodeAdornments(p.Owner);
    }
}
```

- [ ] **Step 4: Run the new test in isolation**

Per [[feedback_avalonia_test_flakiness]], run isolated to avoid the canvas-suite parallel flake:

Run: `dotnet test --filter "FullyQualifiedName~NodiumGraphCanvasPortLayoutCacheTests"`
Expected: 1 passed.

- [ ] **Step 5: Run the canvas suite**

Run: `dotnet test --filter "FullyQualifiedName~NodiumGraphCanvas"`
Expected: all green. Existing `NodiumGraphCanvasConnectionCacheTests` should pass — the change only adds an extra cache drop, doesn't remove any.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test`
Expected: all green. If failures appear only under parallel execution and pass in isolation, log them (consistent with [[feedback_avalonia_test_flakiness]]); they're not regressions.

- [ ] **Step 7: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/NodiumGraphCanvasPortLayoutCacheTests.cs
git commit -m "fix(canvas): invalidate connection geometry when Port.AbsolutePosition fires

Previously the canvas only invalidated connection geometry when a node's
X/Y/W/H/Shape changed — port-level position changes were assumed to
imply node geometry changes. PortLayout's auto-distribute breaks that
assumption: an auto AddPort/RemovePort can move existing ports while
the node is stationary. Invalidate the affected node's connection
geometry cache when any of its ports fires AbsolutePosition; Label
changes do not touch the connection cache.

Part of the PortLayout rollout — see docs/plans/2026-05-19-portlayout-design.md."
```

---

## Task 6: User-guide documentation updates

**Goal:** Document the new auto-layout behavior in four user-guide pages so consumers can discover it and avoid the persistence footgun.

**Files:**
- Modify: `docs/userguide/2-how-to/declare-ports-in-axaml.md`
- Modify: `docs/userguide/3-reference/model.md`
- Modify: `docs/userguide/2-how-to/persist-graph-state.md`
- Modify: `docs/userguide/2-how-to/custom-port-provider.md`

**Acceptance Criteria:**
- [ ] `declare-ports-in-axaml.md` has an "Auto-layout" section after the existing Fraction examples, covering: omit-Fraction → auto; the `(i+1)/(N+1)` formula with worked examples for N=2,3,4; the mixed pinned-and-auto case with the "independent" semantic; declaration order determines on-edge position.
- [ ] `model.md` documents `Port.IsAutoFraction` (immutable, set at construction) and the nullable semantics of `PortDefinition.Fraction` / `PortSpec.Fraction` (null = auto).
- [ ] `persist-graph-state.md` includes the persistence callout: serializing only `Anchor.Fraction` reloads as pinned and silently breaks re-layout. Documents the two recommended approaches (persist-by-spec; persist `IsAutoFraction` alongside).
- [ ] `custom-port-provider.md` notes that `FixedPortProvider.AddPort/RemovePort` are not virtual — extension is via implementing `IPortProvider` — and that custom implementations opting into auto-layout should preserve layout-before-events ordering.
- [ ] All four doc files retain their existing frontmatter (`updated:` date bumped to `2026-05-19`).

**Verify:** Manual review of the four files. Optional: render the docs via the existing pipeline if one exists; no automated test gate.

**Steps:**

- [ ] **Step 1: Read each target doc to learn its existing structure**

```bash
# Quick scan — heading structure of each:
head -50 docs/userguide/2-how-to/declare-ports-in-axaml.md
head -50 docs/userguide/3-reference/model.md
head -50 docs/userguide/2-how-to/persist-graph-state.md
head -50 docs/userguide/2-how-to/custom-port-provider.md
```

Note each file's heading style and code-block conventions so the additions match.

- [ ] **Step 2: Update `declare-ports-in-axaml.md`**

After the existing section that introduces `Fraction`, add a new H2 (or H3 depending on the file's depth) — match the existing pattern. Suggested content:

```markdown
## Auto-layout (omit `Fraction`)

Omitting `Fraction` declares intent to auto-layout. The library distributes auto ports
evenly along their edge using the formula `(i + 1) / (N_auto + 1)`, where `i` is the
port's index among auto ports on that edge (in declaration order) and `N_auto` is the
total count of auto ports on that edge.

```xml
<ng:NodeTemplate DataType="local:MyNode">
  <ng:NodeTemplate.Ports>
    <ng:PortDefinition Name="In1" Flow="Input" Edge="Left"/>
    <ng:PortDefinition Name="In2" Flow="Input" Edge="Left"/>
    <ng:PortDefinition Name="In3" Flow="Input" Edge="Left"/>
  </ng:NodeTemplate.Ports>
  <!-- visual template here -->
</ng:NodeTemplate>
```

The three ports above end up at fractions `0.25`, `0.5`, `0.75` — interior to the edge,
never on a corner.

Distribution examples:

| Auto count | Fractions |
|-----------:|---|
| 1 | 0.5 |
| 2 | 0.333, 0.667 |
| 3 | 0.25, 0.5, 0.75 |
| 4 | 0.2, 0.4, 0.6, 0.8 |

### Mixed pinned and auto on the same edge

Pinned ports (explicit `Fraction`) and auto ports can coexist on the same edge. The
semantic is **independent**: pinned ports keep their declared fraction, auto ports
distribute using only the count of auto ports on that edge. Pinned ports do not
participate in the auto math, and auto ports may share a fraction with a pinned port
if you set one up that way — the library accepts the overlap.

```xml
<ng:PortDefinition Name="Top"    Edge="Left" Fraction="0.1"/>
<ng:PortDefinition Name="Mid1"   Edge="Left"/>
<ng:PortDefinition Name="Mid2"   Edge="Left"/>
<ng:PortDefinition Name="Bottom" Edge="Left" Fraction="0.9"/>
```

Mid1 and Mid2 distribute over the whole edge using `(i+1)/3` → 0.333 and 0.667. They
sit between the two pinned ports because the formula naturally places them in that
range when there are only two auto ports, but the library does not look at the pinned
positions to compute auto fractions.

### Runtime add/remove

Adding or removing an auto port through `FixedPortProvider.AddPort` / `RemovePort`
re-runs the distribution on that edge. The provider fires `PortAdded` / `PortRemoved`
after the layout pass completes, so subscribers always observe a fully-laid-out
collection.
```

- [ ] **Step 3: Update `model.md`**

In `docs/userguide/3-reference/model.md`, find the Port reference section. Add or extend the property table / description to include `IsAutoFraction`:

```markdown
### `Port.IsAutoFraction`

`bool`, get-only. `true` if the port's `Anchor.Fraction` is managed by its owning
`FixedPortProvider` (distributed evenly along the edge); `false` if the Fraction was
pinned at construction. Set at construction and immutable afterward.

Constructors:
- `Port(Node, string, PortFlow, PortAnchor)` — pinned, `IsAutoFraction = false`
- `Port(Node, string, PortFlow, PortEdge)` — auto, `IsAutoFraction = true`. Anchor
  seeded to `(edge, 0.5)`; the provider overwrites the Fraction at provider
  construction or on subsequent add/remove of an auto port on the same edge.
```

In the `PortDefinition` / `PortSpec` sections, update the `Fraction` documentation to reflect the nullable semantic:

```markdown
**`Fraction`** (`double?`) — Pinned position on the edge in `[0, 1]`, or `null` to
declare intent to auto-layout. Null defers fraction selection to the owning
`FixedPortProvider`, which distributes auto ports along their edge via the formula
`(i + 1) / (N_auto + 1)`.
```

- [ ] **Step 4: Update `persist-graph-state.md`**

Add a new section (or extend the existing Port-persistence section if one exists):

```markdown
## Persisting ports declared with auto-layout

Auto-layout ports are an intent expressed at registration time, not just a value on
the live `Port`. If you serialize a graph by storing only each port's `Anchor.Fraction`,
auto ports reload as **pinned at the saved fraction** — the `IsAutoFraction` bit is
lost. This silently breaks runtime re-layout: a later `AddPort` / `RemovePort` on the
same edge will skip the reloaded port (because the library believes it's pinned), and
the visual distribution drifts.

Two recommended approaches:

### 1. Persist by spec, not by port (recommended)

Store the original `PortDefinition` / `PortSpec` shape — including null `Fraction` for
auto ports — and rehydrate the graph by re-registering through `NodePortRegistry`,
then letting `Node.EnsureMaterialized` rebuild the ports. Layout re-runs naturally on
load.

### 2. Persist `IsAutoFraction` alongside `Fraction`

If you have a custom port serializer, include the bit:

```csharp
public record SerializedPort(string Name, PortFlow Flow, PortEdge Edge,
                              double? Fraction, bool IsAutoFraction, ...);
```

On rehydrate, branch on `IsAutoFraction`:

```csharp
var port = serialized.IsAutoFraction
    ? new Port(owner, serialized.Name, serialized.Flow, serialized.Edge)
    : new Port(owner, serialized.Name, serialized.Flow,
               new PortAnchor(serialized.Edge, serialized.Fraction!.Value));
```

Then hand the rehydrated ports to `new FixedPortProvider(ports)`. The provider's ctor
runs the layout pass; any saved auto Fraction is overwritten by the distribution
formula, which is the correct behavior — the saved value was a snapshot, not the
intent.
```

- [ ] **Step 5: Update `custom-port-provider.md`**

Add (or extend) a note near the section that introduces `FixedPortProvider`:

```markdown
### Extending the add/remove behavior

`FixedPortProvider.AddPort` and `RemovePort` are **not virtual**. The extension model
is implementing `IPortProvider`, not subclassing.

If you write a custom `IPortProvider` that opts into auto-layout-style behavior
(distributing or re-positioning ports on add/remove), preserve the
**layout-before-events** ordering: complete any layout pass *before* firing the
add/remove event. Subscribers observing a half-laid-out collection mid-event is a
class of bug worth ruling out at the API level.
```

- [ ] **Step 6: Bump the `updated:` frontmatter date on each touched file**

Each user-guide page has YAML frontmatter (per the project convention). Set `updated: 2026-05-19` on:
- `docs/userguide/2-how-to/declare-ports-in-axaml.md`
- `docs/userguide/3-reference/model.md`
- `docs/userguide/2-how-to/persist-graph-state.md`
- `docs/userguide/2-how-to/custom-port-provider.md`

- [ ] **Step 7: Quick proofread**

Read each modified file end-to-end:
- No conflicting old text about "default Fraction is 0.5".
- No broken wikilinks (`[[...]]`) introduced.
- Code blocks use `csharp` / `xml` fences consistently with the rest of the doc.

- [ ] **Step 8: Commit**

```bash
git add docs/userguide/2-how-to/declare-ports-in-axaml.md docs/userguide/3-reference/model.md docs/userguide/2-how-to/persist-graph-state.md docs/userguide/2-how-to/custom-port-provider.md
git commit -m "docs(userguide): document PortLayout auto-distribute behavior and persistence

declare-ports-in-axaml: auto-layout section, formula, mixed pin/auto
case, runtime re-layout.
model: Port.IsAutoFraction, nullable Fraction semantics.
persist-graph-state: persistence callout — saving only Anchor.Fraction
loses the auto/pinned intent and silently breaks re-layout. Two
recommended approaches documented.
custom-port-provider: note AddPort/RemovePort aren't virtual; custom
IPortProvider implementations opting into auto-layout should preserve
layout-before-events ordering.

Closes the PortLayout rollout — see docs/plans/2026-05-19-portlayout-design.md."
```

---

## Self-Review Checklist (for the engineer running this plan)

Before declaring the plan complete, walk through this once:

- [ ] **Spec coverage:** Every locked-in decision and required artifact in `2026-05-19-portlayout-design.md` is implemented somewhere in Tasks 1-6.
- [ ] **Public surface delta matches spec:** `PortDefinition.Fraction` → `double?`; `PortSpec.Fraction` → `double?`; `Port.IsAutoFraction` exists; new `Port(Node, string, PortFlow, PortEdge)` ctor; `internal Port.SetAnchor`; `FixedPortProvider.AddPort` / `RemovePort` signatures unchanged; no new public types.
- [ ] **Invariants enforced:** SetAnchor throws on pinned-port and different-edge; structural equality short-circuits idempotent passes.
- [ ] **Event ordering:** `PortAdded` / `PortRemoved` fire after `DistributeAuto`.
- [ ] **Canvas delta:** `OnPortPropertyChanged` invalidates connection geometry on `AbsolutePosition` (not on `Label`).
- [ ] **Documentation:** All four user-guide pages updated; `updated:` frontmatter bumped.
- [ ] **Tests:** New test files exist (`PortSetAnchorTests`, `FixedPortProviderLayoutTests`, `NodiumGraphCanvasPortLayoutCacheTests`); extended existing files (`NodePortRegistryTests`, `NodeRegistryMaterializationTests`).
- [ ] **No regressions:** `dotnet test` is green at every task boundary.

---

## Related

- Spec: [[2026-05-19-portlayout-design]] (commit `4e12baf`).
- Upstream feature: [[2026-05-14-declarative-axaml-ports-design]].
- Anchor positioning: [[2026-05-13-anchor-based-port-positioning-design]].
- Test-suite flake caveat: [[feedback_avalonia_test_flakiness]].
