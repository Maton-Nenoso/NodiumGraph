---
title: Declarative AXAML port definitions — implementation plan
tags: [plan]
status: active
created: 2026-05-14
updated: 2026-05-14
---

# Declarative AXAML port definitions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an AXAML form for declaring per-type node port topology that is materialized into the model lazily, so consumers can write `<ng:NodeTemplate>` with `<NodeTemplate.Ports>` and use those ports in code immediately after `InitializeComponent()` returns.

**Architecture:** Per [[2026-05-14-declarative-axaml-ports-design]]. A new `NodeTemplate : IDataTemplate, ISupportInitialize` registers `(Type → IReadOnlyList<PortSpec>)` into a static `NodePortRegistry` at XAML parse (via `EndInit`). `Node.Ports` and `Node.PortProvider` getters lazy-materialize a `FixedPortProvider` from the registry on first read, gated by a `_portProviderExplicit` sentinel so explicit assignment (including `= null`) wins permanently. Exact-type matching everywhere. `NodiumGraphCanvas.AddNodeContainer` is reordered (read provider before subscribing to PropertyChanged) and `AttachProvider` becomes idempotent against double-attach.

**Tech Stack:** C# 12 / .NET 10, Avalonia 12, xUnit v3, `Avalonia.Headless.XUnit` (`[AvaloniaFact]`).

---

## File Structure

```
src/NodiumGraph/
├── PortSpec.cs                                # NEW   — public readonly record struct
├── NodePortRegistry.cs                        # NEW   — static, thread-safe, exact-type lookup
├── Controls/
│   ├── PortDefinition.cs                      # NEW   — POCO, XAML-side construction recipe
│   ├── NodeTemplate.cs                        # NEW   — IDataTemplate, ISupportInitialize
│   └── NodiumGraphCanvas.cs                   # EDIT  — AddNodeContainer reorder + AttachProvider idempotency
└── Model/
    └── Node.cs                                # EDIT  — Ports prop, PortProvider lazy getter, sentinel

tests/NodiumGraph.Tests/
├── NodePortRegistryTests.cs                   # NEW   — pure-model registry tests
├── NodeRegistryMaterializationTests.cs        # NEW   — pure-model lazy-materialization tests
├── CanvasMaterializationTests.cs              # NEW   — Avalonia headless: no-double-attach regression
└── DeclarativeNodeTemplateTests.cs            # NEW   — Avalonia headless: AXAML integration

docs/userguide/2-how-to/
├── declare-ports-in-axaml.md                  # NEW
├── custom-node-template.md                    # EDIT  — cross-ref the new how-to
└── custom-port-provider.md                    # EDIT  — note AXAML form as recommended for fixed sets

docs/userguide/3-reference/
└── strategies.md                              # EDIT  — short NodePortRegistry paragraph
```

---

## Task 1: Data types — `PortSpec` and `PortDefinition`

**Goal:** Add the two value-shaped types the rest of the design needs. Both are trivial enough that they need no dedicated tests; they're exercised by every later task.

**Files:**
- Create: `src/NodiumGraph/PortSpec.cs`
- Create: `src/NodiumGraph/Controls/PortDefinition.cs`

**Acceptance Criteria:**
- [ ] `NodiumGraph.PortSpec` is a public `readonly record struct` with fields `Name`, `Flow`, `Edge`, `Fraction`, `Label`, `MaxConnections`, `DataType` in that order.
- [ ] `NodiumGraph.Controls.PortDefinition` is a public sealed class with settable properties matching the same field set, with defaults: `Name = ""`, `Flow = PortFlow.Input`, `Edge = PortEdge.Left`, `Fraction = 0.5`, others null.
- [ ] `dotnet build` succeeds.

**Verify:** `dotnet build src/NodiumGraph/NodiumGraph.csproj` → no errors.

**Steps:**

- [ ] **Step 1: Create `PortSpec.cs`**

```csharp
// src/NodiumGraph/PortSpec.cs
using NodiumGraph.Model;

namespace NodiumGraph;

/// <summary>
/// Immutable snapshot of a single port declaration in <see cref="NodePortRegistry"/>.
/// Returned from <see cref="NodePortRegistry.TryGet"/> and consumed by <see cref="Model.Node"/>'s
/// lazy materializer.
/// </summary>
public readonly record struct PortSpec(
    string Name,
    PortFlow Flow,
    PortEdge Edge,
    double Fraction,
    string? Label,
    uint? MaxConnections,
    object? DataType);
```

- [ ] **Step 2: Create `PortDefinition.cs`**

```csharp
// src/NodiumGraph/Controls/PortDefinition.cs
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// XAML-side construction recipe for a single port. A list of <see cref="PortDefinition"/>
/// appears under <c>&lt;ng:NodeTemplate.Ports&gt;</c>; <see cref="NodePortRegistry"/> projects each
/// instance into a <see cref="PortSpec"/> at registration time.
/// </summary>
public sealed class PortDefinition
{
    public string Name { get; set; } = string.Empty;
    public PortFlow Flow { get; set; } = PortFlow.Input;
    public PortEdge Edge { get; set; } = PortEdge.Left;
    public double Fraction { get; set; } = 0.5;

    public string? Label { get; set; }
    public uint? MaxConnections { get; set; }
    public object? DataType { get; set; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/NodiumGraph/NodiumGraph.csproj`
Expected: build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/NodiumGraph/PortSpec.cs src/NodiumGraph/Controls/PortDefinition.cs
git commit -m "feat: add PortSpec record and PortDefinition POCO"
```

---

## Task 2: `NodePortRegistry` with full unit-test coverage

**Goal:** A static, thread-safe registry that validates, snapshot-clones, and stores per-type port lists. Exact-type lookup, atomic conflict policy, immutable snapshots.

**Files:**
- Create: `src/NodiumGraph/NodePortRegistry.cs`
- Create: `tests/NodiumGraph.Tests/NodePortRegistryTests.cs`

**Acceptance Criteria:**
- [ ] `NodePortRegistry.Register(Type, IEnumerable<PortDefinition>)` validates inputs and stores a `ReadOnlyCollection<PortSpec>` snapshot.
- [ ] `NodePortRegistry.Register(NodeTemplate)` is a convenience overload (added in Task 5; declare a stub here that throws `NotImplementedException` for now? — no, we add only what we test. Skip this overload until Task 5 needs it.)
- [ ] `NodePortRegistry.TryGet(Type, out IReadOnlyList<PortSpec>)` returns true for an exact-registered type and false otherwise (no walk-up).
- [ ] `NodePortRegistry.Clear()` empties the store atomically.
- [ ] Re-registering with a structurally-identical list is a silent no-op; with a different list throws `InvalidOperationException` whose message contains both lists.
- [ ] Validation throws on: null/empty `Name`, duplicate `Name` within list, out-of-range `Fraction`, undefined `PortEdge`, or `DataType` that is a reference type other than `string`/`Type`.
- [ ] `Register` is atomic against concurrent callers (private `_writeLock`); `TryGet` is lock-free.
- [ ] Snapshots are non-downcastable — `IReadOnlyList<PortSpec>` returned by `TryGet` cannot be cast to `List<PortSpec>` to mutate the registry.

**Verify:** `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~NodePortRegistryTests"` → all tests pass.

**Steps:**

- [ ] **Step 1: Write the failing test file**

Create `tests/NodiumGraph.Tests/NodePortRegistryTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class NodePortRegistryTests
{
    public NodePortRegistryTests() => NodePortRegistry.Clear();

    private sealed class NodeA : Node { }
    private sealed class NodeB : Node { }
    private sealed class DerivedA : NodeA { }

    private static PortDefinition Def(string name, PortFlow flow = PortFlow.Input,
                                      PortEdge edge = PortEdge.Left, double fraction = 0.5,
                                      string? label = null, uint? maxConnections = null,
                                      object? dataType = null)
        => new() { Name = name, Flow = flow, Edge = edge, Fraction = fraction,
                   Label = label, MaxConnections = maxConnections, DataType = dataType };

    [Fact]
    public void TryGet_returns_registered_snapshot()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in"), Def("out", PortFlow.Output, PortEdge.Right) });
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("in",  snapshot[0].Name);
        Assert.Equal("out", snapshot[1].Name);
        Assert.Equal(PortFlow.Output, snapshot[1].Flow);
        Assert.Equal(PortEdge.Right,  snapshot[1].Edge);
    }

    [Fact]
    public void TryGet_unregistered_type_returns_false()
    {
        Assert.False(NodePortRegistry.TryGet(typeof(NodeB), out var snapshot));
        Assert.Empty(snapshot);
    }

    [Fact]
    public void TryGet_does_not_walk_to_base_type()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        Assert.False(NodePortRegistry.TryGet(typeof(DerivedA), out _));
    }

    [Fact]
    public void Register_identical_list_is_silent_no_op()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in"), Def("out", PortFlow.Output, PortEdge.Right) });
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in"), Def("out", PortFlow.Output, PortEdge.Right) });
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Equal(2, snapshot.Count);
    }

    [Fact]
    public void Register_different_list_throws()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("out", PortFlow.Output, PortEdge.Right) }));
        Assert.Contains("in",  ex.Message);
        Assert.Contains("out", ex.Message);
    }

    [Fact]
    public void Register_rejects_null_type()
        => Assert.Throws<ArgumentNullException>(() => NodePortRegistry.Register(null!, new[] { Def("in") }));

    [Fact]
    public void Register_rejects_non_node_type()
        => Assert.Throws<ArgumentException>(() => NodePortRegistry.Register(typeof(string), new[] { Def("in") }));

    [Fact]
    public void Register_rejects_empty_name()
        => Assert.Throws<ArgumentException>(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("") }));

    [Fact]
    public void Register_rejects_duplicate_names()
        => Assert.Throws<ArgumentException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x"), Def("x", PortFlow.Output) }));

    [Fact]
    public void Register_rejects_out_of_range_fraction()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", fraction: 1.5) }));

    [Fact]
    public void Register_rejects_undefined_edge()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", edge: (PortEdge)42) }));

    [Theory]
    [InlineData(null)]
    [InlineData("x")]
    [InlineData(42)]                             // primitive
    public void Register_accepts_allowed_DataType_values(object? dt)
        => NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: dt) });

    [Fact]
    public void Register_accepts_Type_as_DataType()
        => NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: typeof(int)) });

    [Fact]
    public void Register_accepts_enum_as_DataType()
        => NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: PortFlow.Input) });

    [Fact]
    public void Register_rejects_class_DataType()
        => Assert.Throws<ArgumentException>(() =>
            NodePortRegistry.Register(typeof(NodeA), new[] { Def("x", dataType: new object()) }));

    [Fact]
    public void Snapshot_decoupled_from_source_PortDefinitions()
    {
        var def = Def("in");
        NodePortRegistry.Register(typeof(NodeA), new[] { def });
        def.Name = "mutated";
        NodePortRegistry.TryGet(typeof(NodeA), out var snapshot);
        Assert.Equal("in", snapshot[0].Name);
    }

    [Fact]
    public void Snapshot_decoupled_from_source_list_mutation()
    {
        var list = new List<PortDefinition> { Def("in") };
        NodePortRegistry.Register(typeof(NodeA), list);
        list.Clear();
        NodePortRegistry.TryGet(typeof(NodeA), out var snapshot);
        Assert.Single(snapshot);
    }

    [Fact]
    public void Snapshot_is_not_castable_to_writable_List()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        NodePortRegistry.TryGet(typeof(NodeA), out var snapshot);
        Assert.IsNotType<List<PortSpec>>(snapshot);
    }

    [Fact]
    public void Clear_empties_the_registry()
    {
        NodePortRegistry.Register(typeof(NodeA), new[] { Def("in") });
        NodePortRegistry.Clear();
        Assert.False(NodePortRegistry.TryGet(typeof(NodeA), out _));
    }

    [Fact]
    public async Task Concurrent_register_with_different_defs_throws_on_one_thread()
    {
        var t1 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("a") }));
        var t2 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("b") }));
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(t1, t2));
        // At least one of the two must have failed; the other succeeded.
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Single(snapshot);
    }

    [Fact]
    public async Task Concurrent_register_with_identical_defs_both_succeed()
    {
        var t1 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("a") }));
        var t2 = Task.Run(() => NodePortRegistry.Register(typeof(NodeA), new[] { Def("a") }));
        await Task.WhenAll(t1, t2);
        Assert.True(NodePortRegistry.TryGet(typeof(NodeA), out var snapshot));
        Assert.Single(snapshot);
    }
}

[CollectionDefinition("NodePortRegistry", DisableParallelization = true)]
public class NodePortRegistryCollection { }
```

- [ ] **Step 2: Run tests to confirm they fail to compile**

Run: `dotnet build tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
Expected: compile errors — `NodePortRegistry` not found.

- [ ] **Step 3: Implement `NodePortRegistry`**

Create `src/NodiumGraph/NodePortRegistry.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using NodiumGraph.Controls;
using NodiumGraph.Model;

namespace NodiumGraph;

/// <summary>
/// Static, process-wide registry mapping a concrete <see cref="Node"/> subtype to its
/// declared port topology. Populated by <see cref="NodeTemplate"/> at XAML parse time
/// (via <see cref="System.ComponentModel.ISupportInitialize.EndInit"/>) and consulted by
/// <see cref="Node.PortProvider"/>/<see cref="Node.Ports"/> on first read.
/// </summary>
public static class NodePortRegistry
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PortSpec>> _store = new();
    private static readonly object _writeLock = new();

    /// <summary>
    /// Registers a port topology for <paramref name="nodeType"/>. Validates inputs, projects each
    /// <see cref="PortDefinition"/> into an immutable <see cref="PortSpec"/>, and stores the result.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="nodeType"/> or <paramref name="definitions"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="nodeType"/> isn't a <see cref="Node"/> subtype, or a definition has an invalid Name/DataType.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If a definition has an out-of-range Fraction or undefined Edge.</exception>
    /// <exception cref="InvalidOperationException">If a different list is already registered for the same type.</exception>
    public static void Register(Type nodeType, IEnumerable<PortDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!typeof(Node).IsAssignableFrom(nodeType))
            throw new ArgumentException($"{nodeType.FullName} is not assignable to {nameof(Node)}.", nameof(nodeType));

        var snapshot = BuildSnapshot(definitions);

        lock (_writeLock)
        {
            if (_store.TryGetValue(nodeType, out var existing))
            {
                if (!StructurallyEqual(existing, snapshot))
                    throw new InvalidOperationException(BuildConflictMessage(nodeType, existing, snapshot));
                return;
            }
            _store[nodeType] = snapshot;
        }
    }

    /// <summary>
    /// Exact-type lookup. Returns false (with an empty snapshot) if no entry exists for
    /// <paramref name="nodeType"/>; does not walk base types.
    /// </summary>
    public static bool TryGet(Type nodeType, out IReadOnlyList<PortSpec> snapshot)
    {
        if (_store.TryGetValue(nodeType, out var stored))
        {
            snapshot = stored;
            return true;
        }
        snapshot = Array.Empty<PortSpec>();
        return false;
    }

    /// <summary>Removes all registrations. Already-materialized <see cref="Node"/> instances keep their providers.</summary>
    public static void Clear()
    {
        lock (_writeLock) _store.Clear();
    }

    private static ReadOnlyCollection<PortSpec> BuildSnapshot(IEnumerable<PortDefinition> definitions)
    {
        var specs = new List<PortSpec>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var d in definitions)
        {
            ArgumentNullException.ThrowIfNull(d, nameof(definitions));
            if (string.IsNullOrEmpty(d.Name))
                throw new ArgumentException("PortDefinition.Name must be non-null and non-empty.", nameof(definitions));
            if (!names.Add(d.Name))
                throw new ArgumentException($"Duplicate port name '{d.Name}'.", nameof(definitions));
            if (double.IsNaN(d.Fraction) || d.Fraction < 0.0 || d.Fraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(definitions), $"Fraction {d.Fraction} for '{d.Name}' is not in [0,1].");
            if (d.Edge is not (PortEdge.Left or PortEdge.Top or PortEdge.Right or PortEdge.Bottom))
                throw new ArgumentOutOfRangeException(nameof(definitions), $"Undefined PortEdge value {(int)d.Edge} for '{d.Name}'.");
            ValidateDataType(d.DataType, d.Name);

            specs.Add(new PortSpec(d.Name, d.Flow, d.Edge, d.Fraction, d.Label, d.MaxConnections, d.DataType));
        }

        return new ReadOnlyCollection<PortSpec>(specs);
    }

    private static void ValidateDataType(object? dataType, string portName)
    {
        if (dataType is null) return;
        if (dataType is string or Type) return;
        if (dataType.GetType().IsValueType) return;
        throw new ArgumentException(
            $"PortDefinition.DataType for '{portName}' must be null, a string, a System.Type, or a value type; got {dataType.GetType().FullName}.",
            nameof(dataType));
    }

    private static bool StructurallyEqual(IReadOnlyList<PortSpec> a, IReadOnlyList<PortSpec> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }

    private static string BuildConflictMessage(Type nodeType, IReadOnlyList<PortSpec> existing, IReadOnlyList<PortSpec> incoming)
    {
        var sb = new StringBuilder();
        sb.Append("Conflicting NodePortRegistry registration for ").Append(nodeType.FullName).AppendLine(".");
        sb.AppendLine("Existing:");
        foreach (var s in existing) sb.Append("  ").AppendLine(s.ToString());
        sb.AppendLine("Incoming:");
        foreach (var s in incoming) sb.Append("  ").AppendLine(s.ToString());
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~NodePortRegistryTests"`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/NodePortRegistry.cs tests/NodiumGraph.Tests/NodePortRegistryTests.cs
git commit -m "feat: add NodePortRegistry with validation, snapshot cloning, and conflict policy"
```

---

## Task 3: `Node` lazy materialization

**Goal:** Add `Node.Ports`, change `Node.PortProvider` getter to lazy-materialize from `NodePortRegistry`, and gate both with a `_portProviderExplicit` sentinel so any setter call (including `= null`) wins permanently.

**Files:**
- Modify: `src/NodiumGraph/Model/Node.cs`
- Create: `tests/NodiumGraph.Tests/NodeRegistryMaterializationTests.cs`

**Acceptance Criteria:**
- [ ] `node.Ports` returns the materialized port list (or empty when no provider exists).
- [ ] First read of `node.PortProvider` (or `node.Ports`) on a type registered in `NodePortRegistry` returns a freshly-built `FixedPortProvider`.
- [ ] Pre-assigning `node.PortProvider = customProvider` short-circuits the registry on subsequent reads.
- [ ] Assigning `node.PortProvider = null` permanently suppresses the registry — subsequent reads do not re-materialize.
- [ ] `Label`, `MaxConnections`, and `DataType` from the registry snapshot propagate to the materialized `Port`.
- [ ] Materialization fires `PropertyChanged(nameof(PortProvider))` exactly once on first access.
- [ ] After `NodePortRegistry.Clear()`, an already-materialized node keeps its provider.

**Verify:** `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~NodeRegistryMaterializationTests"` → all tests pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `tests/NodiumGraph.Tests/NodeRegistryMaterializationTests.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class NodeRegistryMaterializationTests
{
    public NodeRegistryMaterializationTests() => NodePortRegistry.Clear();

    private sealed class TypeA : Node { }
    private sealed class TypeB : Node { }

    private static PortDefinition Def(string name, PortFlow flow = PortFlow.Input,
                                      PortEdge edge = PortEdge.Left, double fraction = 0.5,
                                      string? label = null, uint? maxConnections = null, object? dataType = null)
        => new() { Name = name, Flow = flow, Edge = edge, Fraction = fraction,
                   Label = label, MaxConnections = maxConnections, DataType = dataType };

    [Fact]
    public void Ports_materializes_from_registry()
    {
        NodePortRegistry.Register(typeof(TypeA), new[]
        {
            Def("in",  PortFlow.Input,  PortEdge.Left,  0.5),
            Def("out", PortFlow.Output, PortEdge.Right, 0.5),
        });
        var node = new TypeA { Width = 100, Height = 50 };

        var ports = node.Ports;

        Assert.Equal(2, ports.Count);
        Assert.Equal("in",  ports[0].Name);
        Assert.Equal("out", ports[1].Name);
        Assert.IsType<FixedPortProvider>(node.PortProvider);
    }

    [Fact]
    public void PortProvider_getter_also_triggers_materialization()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };

        var provider = node.PortProvider;

        Assert.NotNull(provider);
        Assert.Single(provider!.Ports);
    }

    [Fact]
    public void Pre_assigned_provider_wins_over_registry()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        var custom = new FixedPortProvider();
        node.PortProvider = custom;

        Assert.Same(custom, node.PortProvider);
        Assert.Empty(node.Ports);
    }

    [Fact]
    public void Explicit_null_suppresses_registry_permanently()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        node.PortProvider = null;

        Assert.Null(node.PortProvider);
        Assert.Empty(node.Ports);
    }

    [Fact]
    public void Unregistered_type_stays_portless()
    {
        var node = new TypeB { Width = 100, Height = 50 };
        Assert.Empty(node.Ports);
        Assert.Null(node.PortProvider);
    }

    [Fact]
    public void Late_registration_is_picked_up_on_next_access()
    {
        var node = new TypeA { Width = 100, Height = 50 };
        Assert.Empty(node.Ports);

        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });

        Assert.Single(node.Ports);
        Assert.NotNull(node.PortProvider);
    }

    [Fact]
    public void Optional_fields_propagate_to_materialized_Port()
    {
        NodePortRegistry.Register(typeof(TypeA), new[]
        {
            Def("x", label: "Label", maxConnections: 3u, dataType: "number"),
        });
        var node = new TypeA { Width = 100, Height = 50 };

        var port = node.Ports.Single();

        Assert.Equal("Label",  port.Label);
        Assert.Equal(3u,       port.MaxConnections);
        Assert.Equal("number", port.DataType);
    }

    [Fact]
    public void Repeated_Ports_access_does_not_re_materialize()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };

        var first  = node.Ports;
        var second = node.Ports;

        Assert.Same(first, second);
    }

    [Fact]
    public void Materialization_fires_PropertyChanged_once()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        var fires = new List<string?>();
        node.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Node.PortProvider)) fires.Add(e.PropertyName); };

        _ = node.Ports;
        _ = node.Ports;
        _ = node.PortProvider;

        Assert.Single(fires);
    }

    [Fact]
    public void Clear_does_not_affect_already_materialized_node()
    {
        NodePortRegistry.Register(typeof(TypeA), new[] { Def("in") });
        var node = new TypeA { Width = 100, Height = 50 };
        var providerBefore = node.PortProvider;

        NodePortRegistry.Clear();

        Assert.Same(providerBefore, node.PortProvider);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~NodeRegistryMaterializationTests"`
Expected: fails — `node.Ports` doesn't exist, registry materialization isn't wired.

- [ ] **Step 3: Modify `Node.cs`**

Open `src/NodiumGraph/Model/Node.cs`. Make these changes inside the `Node` class.

Add a new field next to `_portProvider` (around line 23):

```csharp
private bool _portProviderExplicit;
```

Replace the existing `PortProvider` property (the one with `get => _portProvider; set => SetField(...)`) with:

```csharp
public IPortProvider? PortProvider
{
    get
    {
        EnsureMaterialized();
        return _portProvider;
    }
    set
    {
        _portProviderExplicit = true;     // any assignment (including null) suppresses registry defaults
        SetField(ref _portProvider, value);
    }
}
```

Add a new property right below `PortProvider`:

```csharp
/// <summary>
/// All ports owned by this node. Equivalent to <c>PortProvider?.Ports</c> — but also triggers
/// lazy materialization from <see cref="NodePortRegistry"/> on first access when the consumer
/// has not assigned a provider in code.
/// </summary>
public IReadOnlyList<Port> Ports
{
    get
    {
        EnsureMaterialized();
        return _portProvider?.Ports ?? Array.Empty<Port>();
    }
}
```

Add the private materializer near the bottom of the class (before the `INotifyPropertyChanged` plumbing):

```csharp
private void EnsureMaterialized()
{
    if (_portProviderExplicit) return;
    if (!NodePortRegistry.TryGet(GetType(), out var specs)) return;

    var ports = specs.Select(s => new Port(this, s.Name, s.Flow, new PortAnchor(s.Edge, s.Fraction))
    {
        Label = s.Label,
        MaxConnections = s.MaxConnections,
        DataType = s.DataType,
    }).ToList();

    PortProvider = new FixedPortProvider(ports);   // routes through setter → flips sentinel, raises PropertyChanged
}
```

Add the required `using` directives at the top of the file (the `using` block):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~NodeRegistryMaterializationTests"`
Expected: all tests pass.

- [ ] **Step 5: Run full test suite to verify no regressions**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
Expected: all tests pass (existing 684 + new tests from Tasks 2–3).

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/Node.cs tests/NodiumGraph.Tests/NodeRegistryMaterializationTests.cs
git commit -m "feat: lazy-materialize Node.PortProvider from NodePortRegistry"
```

---

## Task 4: `NodiumGraphCanvas` — `AddNodeContainer` reorder + `AttachProvider` idempotency

**Goal:** Prevent the double-attach that the new lazy `PortProvider` getter would cause if the canvas reads `node.PortProvider` after subscribing to `PropertyChanged`. Reorder `AddNodeContainer` so the read precedes the subscription, and harden `AttachProvider` to be idempotent against the same `(node, provider)` pair.

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (`AddNodeContainer` ~lines 1753-1780; `AttachProvider` ~lines 1811-1831)
- Create: `tests/NodiumGraph.Tests/CanvasMaterializationTests.cs`

**Acceptance Criteria:**
- [ ] `AddNodeContainer` reads `var provider = node.PortProvider;` before `node.PropertyChanged += OnNodePropertyChanged;`.
- [ ] `AttachProvider` short-circuits when `_nodeProviders` already holds the same provider for the node.
- [ ] When a node with a registry entry is added to the graph, `provider.PortAdded` ends up with exactly one subscriber (no double-attach).
- [ ] Existing canvas tests continue to pass.

**Verify:** `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~CanvasMaterializationTests"` → all tests pass, plus the full suite stays green.

**Steps:**

- [ ] **Step 1: Write the failing test**

Create `tests/NodiumGraph.Tests/CanvasMaterializationTests.cs`:

```csharp
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class CanvasMaterializationTests
{
    public CanvasMaterializationTests() => NodePortRegistry.Clear();

    private sealed class CanvasNodeA : Node { }

    [AvaloniaFact]
    public void Adding_node_with_registry_entry_attaches_provider_exactly_once()
    {
        NodePortRegistry.Register(typeof(CanvasNodeA), new[]
        {
            new PortDefinition { Name = "in",  Flow = PortFlow.Input,  Edge = PortEdge.Left,  Fraction = 0.5 },
            new PortDefinition { Name = "out", Flow = PortFlow.Output, Edge = PortEdge.Right, Fraction = 0.5 },
        });

        var canvas = new NodiumGraphCanvas();
        var window = new Window { Content = canvas };
        window.Show();

        var graph = new Graph();
        canvas.Graph = graph;

        var node = new CanvasNodeA { Width = 100, Height = 50 };
        graph.AddNode(node);

        var provider = node.PortProvider!;

        // Count PortAdded subscribers. The canvas attaches one lambda per provider; double-attach
        // would put two distinct entries in the invocation list.
        var portAddedField = typeof(FixedPortProvider).GetField("PortAdded",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(portAddedField);
        var portAdded = (System.Action<Port>?)portAddedField!.GetValue(provider);
        var subscribers = portAdded?.GetInvocationList().Length ?? 0;

        Assert.Equal(1, subscribers);
    }
}
```

> Implementation note: `FixedPortProvider.PortAdded` is a public event. The reflection above accesses the underlying delegate field. If the event is declared without an explicit backing field, expose the count via a temporary internal probe property like `internal int PortAddedSubscriberCount => PortAdded?.GetInvocationList().Length ?? 0;` on `FixedPortProvider`, gate it with `#if DEBUG` if you prefer, and adjust the test accordingly.

- [ ] **Step 2: Run test to confirm it fails (double-attach detected)**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~CanvasMaterializationTests"`
Expected: fails with `Assert.Equal(1, 2)` (subscriber count is 2 due to the unfixed double-attach).

- [ ] **Step 3: Reorder `AddNodeContainer`**

In `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`, locate `AddNodeContainer` (around line 1753). Replace this block:

```csharp
        node.PropertyChanged += OnNodePropertyChanged;

        if (node.PortProvider != null)
            AttachProvider(node, node.PortProvider);
```

with:

```csharp
        // Read PortProvider BEFORE subscribing to PropertyChanged. The lazy getter on Node may
        // materialize a registry-backed provider here, which fires PropertyChanged. If our
        // subscription were already in place, OnNodePropertyChanged would attach the provider
        // and the explicit AttachProvider call below would double-attach.
        var provider = node.PortProvider;

        node.PropertyChanged += OnNodePropertyChanged;

        if (provider != null)
            AttachProvider(node, provider);
```

- [ ] **Step 4: Make `AttachProvider` idempotent**

In the same file, locate `AttachProvider` (around line 1811). Add an idempotency guard at the top:

```csharp
    private void AttachProvider(Node node, IPortProvider provider)
    {
        // Defense in depth: any other code path that reads node.PortProvider while a
        // PropertyChanged subscription is live can route us here a second time for the same
        // (node, provider) pair. Short-circuit when we're already attached.
        if (_nodeProviders.TryGetValue(node, out var existing) && ReferenceEquals(existing, provider))
            return;

        foreach (var port in provider.Ports)
            SubscribeToPort(port);

        // ... rest of the existing body unchanged ...
```

Keep the rest of the method body as it is.

- [ ] **Step 5: Run the canvas test to verify it passes**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~CanvasMaterializationTests"`
Expected: passes.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
Expected: all tests pass. Verify no existing canvas tests have regressed.

- [ ] **Step 7: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/CanvasMaterializationTests.cs
git commit -m "fix(canvas): reorder AddNodeContainer and make AttachProvider idempotent"
```

---

## Task 5: `NodeTemplate` and AXAML integration tests

**Goal:** Add the `<ng:NodeTemplate>` AXAML surface — an `IDataTemplate` with exact-type `Match`, a `Ports` collection property, and `ISupportInitialize.EndInit` that registers `(DataType, Ports)` into `NodePortRegistry`. Cover the full AXAML loading path with headless tests.

**Files:**
- Create: `src/NodiumGraph/Controls/NodeTemplate.cs`
- Modify: `src/NodiumGraph/NodePortRegistry.cs` (add `Register(NodeTemplate)` overload)
- Create: `tests/NodiumGraph.Tests/DeclarativeNodeTemplateTests.cs`
- Create: `tests/NodiumGraph.Tests/Helpers/DeclarativePortsTestWindow.axaml`
- Create: `tests/NodiumGraph.Tests/Helpers/DeclarativePortsTestWindow.axaml.cs`

**Acceptance Criteria:**
- [ ] `NodeTemplate` implements `IDataTemplate` and `ISupportInitialize`.
- [ ] `NodeTemplate.Match(data)` returns true only when `DataType == data?.GetType()` (exact type).
- [ ] `NodeTemplate.Build(...)` produces the visual from its `Content` template slot.
- [ ] `NodeTemplate.EndInit` no-ops when `DataType` is null or `Ports.Count == 0`; otherwise calls `NodePortRegistry.Register(DataType, Ports)`.
- [ ] After loading the test window, the registry has the declared entries.
- [ ] A `Node` of the templated type, constructed after window load, has materialized ports.
- [ ] A derived type (no own registration) is NOT matched by the base `NodeTemplate` (exact-type test).
- [ ] A visual-only `<ng:NodeTemplate>` (no `Ports`) does not register and leaves `PortProvider` null.

**Verify:** `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~DeclarativeNodeTemplateTests"` → all pass.

**Steps:**

- [ ] **Step 1: Add the `NodeTemplate` overload to `NodePortRegistry`**

In `src/NodiumGraph/NodePortRegistry.cs`, add a convenience overload below the existing `Register(Type, IEnumerable<PortDefinition>)`:

```csharp
/// <summary>
/// Convenience overload used by <see cref="NodeTemplate"/>. Equivalent to
/// <c>Register(template.DataType, template.Ports)</c> with a null check.
/// </summary>
public static void Register(NodeTemplate template)
{
    ArgumentNullException.ThrowIfNull(template);
    if (template.DataType is null)
        throw new ArgumentException("NodeTemplate.DataType must be set before registration.", nameof(template));
    Register(template.DataType, template.Ports);
}
```

- [ ] **Step 2: Create `NodeTemplate.cs`**

```csharp
// src/NodiumGraph/Controls/NodeTemplate.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;

namespace NodiumGraph.Controls;

/// <summary>
/// IDataTemplate variant that pairs a per-CLR-type visual template with declarative port
/// topology. On XAML load (<see cref="ISupportInitialize.EndInit"/>), registers
/// <see cref="DataType"/> → <see cref="Ports"/> into <see cref="NodePortRegistry"/> when both
/// are populated.
/// </summary>
public sealed class NodeTemplate : IDataTemplate, ISupportInitialize
{
    public Type? DataType { get; set; }

    public IList<PortDefinition> Ports { get; } = new List<PortDefinition>();

    [Content]
    [TemplateContent(TemplateResultType = typeof(Control))]
    public object? Content { get; set; }

    public bool Match(object? data) => data != null && DataType == data.GetType();

    public Control? Build(object? param)
        => Content is null ? null : TemplateContent.Load<Control>(Content)?.Result;

    void ISupportInitialize.BeginInit() { }

    void ISupportInitialize.EndInit()
    {
        if (DataType is null) return;
        if (Ports.Count == 0) return;
        NodePortRegistry.Register(this);
    }
}
```

> If a build error surfaces from `[TemplateContent]` or `TemplateContent.Load`, verify the namespace via the Avalonia docs MCP (`lookup_avalonia_api`) — Avalonia 12 has had churn around `TemplateContent`. The fallback is to register on first `Match`/`Build` call instead of `EndInit`, as the spec calls out.

- [ ] **Step 3: Create the test window AXAML**

Create `tests/NodiumGraph.Tests/Helpers/DeclarativePortsTestWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"
        xmlns:tst="clr-namespace:NodiumGraph.Tests.Helpers"
        x:Class="NodiumGraph.Tests.Helpers.DeclarativePortsTestWindow"
        Title="Declarative Ports Test"
        Width="200" Height="100">

  <Window.DataTemplates>

    <!-- Two ports declared on the base concrete node type -->
    <ng:NodeTemplate DataType="tst:DeclarativeNodeA">
      <ng:NodeTemplate.Ports>
        <ng:PortDefinition Name="in"  Flow="Input"  Edge="Left"  Fraction="0.5" />
        <ng:PortDefinition Name="out" Flow="Output" Edge="Right" Fraction="0.5" Label="result" />
      </ng:NodeTemplate.Ports>
      <ng:NodePresenter HeaderBackground="#10B981">
        <TextBlock Text="Node A" />
      </ng:NodePresenter>
    </ng:NodeTemplate>

    <!-- Visual-only NodeTemplate: no Ports element. Must not register. -->
    <ng:NodeTemplate DataType="tst:DeclarativeNodeVisualOnly">
      <ng:NodePresenter HeaderBackground="#3B82F6">
        <TextBlock Text="Visual only" />
      </ng:NodePresenter>
    </ng:NodeTemplate>

  </Window.DataTemplates>

  <ng:NodiumGraphCanvas x:Name="Canvas" />
</Window>
```

Create `tests/NodiumGraph.Tests/Helpers/DeclarativePortsTestWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NodiumGraph.Model;

namespace NodiumGraph.Tests.Helpers;

public partial class DeclarativePortsTestWindow : Window
{
    public DeclarativePortsTestWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

public class DeclarativeNodeA : Node { }
public class DeclarativeNodeDerivedA : DeclarativeNodeA { }   // intentionally not registered
public class DeclarativeNodeVisualOnly : Node { }
```

- [ ] **Step 4: Write the failing AXAML integration tests**

Create `tests/NodiumGraph.Tests/DeclarativeNodeTemplateTests.cs`:

```csharp
using System.Linq;
using Avalonia.Headless.XUnit;
using NodiumGraph;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using NodiumGraph.Tests.Helpers;
using Xunit;

namespace NodiumGraph.Tests;

[Collection("NodePortRegistry")]
public class DeclarativeNodeTemplateTests
{
    public DeclarativeNodeTemplateTests() => NodePortRegistry.Clear();

    [AvaloniaFact]
    public void Window_load_populates_registry_for_NodeTemplate_with_Ports()
    {
        _ = new DeclarativePortsTestWindow();

        Assert.True(NodePortRegistry.TryGet(typeof(DeclarativeNodeA), out var specs));
        Assert.Equal(2, specs.Count);
        Assert.Equal("in",  specs[0].Name);
        Assert.Equal("out", specs[1].Name);
        Assert.Equal("result", specs[1].Label);
    }

    [AvaloniaFact]
    public void Window_load_does_not_register_visual_only_NodeTemplate()
    {
        _ = new DeclarativePortsTestWindow();

        Assert.False(NodePortRegistry.TryGet(typeof(DeclarativeNodeVisualOnly), out _));
    }

    [AvaloniaFact]
    public void Node_constructed_after_window_load_has_materialized_ports()
    {
        _ = new DeclarativePortsTestWindow();

        var node = new DeclarativeNodeA { Width = 100, Height = 50 };

        Assert.Equal(2, node.Ports.Count);
        Assert.NotNull(node.PortProvider);
    }

    [AvaloniaFact]
    public void Derived_type_with_no_own_registration_does_not_inherit_base_template_ports()
    {
        _ = new DeclarativePortsTestWindow();

        var node = new DeclarativeNodeDerivedA { Width = 100, Height = 50 };

        Assert.Empty(node.Ports);
        Assert.Null(node.PortProvider);
    }

    [AvaloniaFact]
    public void NodeTemplate_Match_is_exact_type_only()
    {
        var template = new NodeTemplate { DataType = typeof(DeclarativeNodeA) };

        Assert.True(template.Match(new DeclarativeNodeA()));
        Assert.False(template.Match(new DeclarativeNodeDerivedA()));
    }

    [AvaloniaFact]
    public void Canvas_render_path_attaches_materialized_provider_via_graph_binding()
    {
        var window = new DeclarativePortsTestWindow();
        window.Show();
        var canvas = window.FindControl<NodiumGraphCanvas>("Canvas")!;

        var graph = new Graph();
        canvas.Graph = graph;

        var node = new DeclarativeNodeA { Width = 100, Height = 50 };
        graph.AddNode(node);

        Assert.NotNull(node.PortProvider);
        Assert.Equal(2, node.PortProvider!.Ports.Count);
    }
}
```

- [ ] **Step 5: Run tests to confirm they fail (or fail to compile)**

Run: `dotnet build tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
Expected: compile error — `NodeTemplate` missing or AXAML symbols missing.

- [ ] **Step 6: Build the library**

Run: `dotnet build src/NodiumGraph/NodiumGraph.csproj`
Expected: build succeeded with `NodeTemplate.cs` and the overload added.

- [ ] **Step 7: Build and run tests**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj --filter "FullyQualifiedName~DeclarativeNodeTemplateTests"`
Expected: all tests pass. If `EndInit` isn't being invoked by the Avalonia XAML loader for the `<ng:NodeTemplate>` instances inside `<Window.DataTemplates>`, fall back to registering on first `Match` call:

```csharp
public bool Match(object? data)
{
    EnsureRegistered();      // idempotent — uses a private bool flag
    return data != null && DataType == data.GetType();
}
```

Add a `private bool _registered;` field and an `EnsureRegistered()` that gates by the flag. Re-run.

- [ ] **Step 8: Run full test suite**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
Expected: all tests pass (existing 684 + new tests from Tasks 2–5).

- [ ] **Step 9: Commit**

```bash
git add src/NodiumGraph/Controls/NodeTemplate.cs \
        src/NodiumGraph/NodePortRegistry.cs \
        tests/NodiumGraph.Tests/Helpers/DeclarativePortsTestWindow.axaml \
        tests/NodiumGraph.Tests/Helpers/DeclarativePortsTestWindow.axaml.cs \
        tests/NodiumGraph.Tests/DeclarativeNodeTemplateTests.cs
git commit -m "feat: add NodeTemplate IDataTemplate with EndInit-time port registration"
```

---

## Task 6: Documentation

**Goal:** Document the new surface for end users — one new how-to, three updates.

**Files:**
- Create: `docs/userguide/2-how-to/declare-ports-in-axaml.md`
- Modify: `docs/userguide/2-how-to/custom-node-template.md`
- Modify: `docs/userguide/2-how-to/custom-port-provider.md`
- Modify: `docs/userguide/3-reference/strategies.md`

**Acceptance Criteria:**
- [ ] `declare-ports-in-axaml.md` exists with the standard YAML frontmatter (`title`, `tags: [how-to]`, `status: active`, `created/updated: 2026-05-14`), an end-to-end example matching the test window, and the "after `InitializeComponent`" rule called out.
- [ ] `custom-node-template.md` cross-references the new how-to and clarifies that plain `<DataTemplate>` (polymorphic) and `<ng:NodeTemplate>` (exact-type, with ports) are both supported.
- [ ] `custom-port-provider.md` notes AXAML declaration as the recommended path for fixed sets and keeps the code-side `FixedPortProvider` example as the override / dynamic path.
- [ ] `strategies.md` gains a short paragraph on `NodePortRegistry`, the "after `InitializeComponent`" rule, and the "code wins" override.

**Verify:** Read each file; markdown lint clean (no broken wikilinks).

**Steps:**

- [ ] **Step 1: Create `declare-ports-in-axaml.md`**

```markdown
---
title: Declare ports in AXAML
tags: [how-to]
status: active
created: 2026-05-14
updated: 2026-05-14
---

# Declare ports in AXAML

When a node CLR type has a fixed set of ports, declare them inline in the AXAML template
that defines the node visual. After `InitializeComponent()` returns, the ports are
accessible from code without realizing any UI.

## Use `<ng:NodeTemplate>` instead of `<DataTemplate>`

```xml
<Window.DataTemplates>
  <ng:NodeTemplate DataType="local:InputSourceNode">
    <ng:NodeTemplate.Ports>
      <ng:PortDefinition Name="out" Flow="Output" Edge="Right" Fraction="0.5" />
    </ng:NodeTemplate.Ports>

    <ng:NodePresenter HeaderBackground="#10B981">
      <TextBlock Text="Reads data from external source" />
    </ng:NodePresenter>
  </ng:NodeTemplate>
</Window.DataTemplates>
```

## Build connections in code

```csharp
public MainWindow()
{
    InitializeComponent();                              // XAML parses, registry populates

    var src    = new InputSourceNode();
    var xform  = new TransformNode();
    var graph  = new Graph();
    graph.AddNode(src);
    graph.AddNode(xform);
    graph.AddConnection(new Connection(
        src.Ports.First(p => p.Name == "out"),
        xform.Ports.First(p => p.Name == "in")));

    Canvas.Graph = graph;
}
```

## Rules to remember

- **Exact-type matching.** `<ng:NodeTemplate DataType="local:Base">` does not apply to derived types.
  Each concrete node type that needs declared ports gets its own `<ng:NodeTemplate>`.
- **Code wins.** Setting `node.PortProvider` (including `= null`) permanently suppresses the
  registry for that node instance.
- **Mixed templates.** Use plain `<DataTemplate>` when you want polymorphic visual matching
  without ports — wire ports in code or in derived `<ng:NodeTemplate>` declarations.

See also: [[custom-node-template]], [[custom-port-provider]].
```

- [ ] **Step 2: Update `custom-node-template.md`**

Read the file first to find the right insertion point, then add a section near the top that introduces `<ng:NodeTemplate>` alongside `<DataTemplate>` and links to `[[declare-ports-in-axaml]]`. Keep the existing content; add 2–3 paragraphs covering:
1. Both `<DataTemplate>` (polymorphic, no ports) and `<ng:NodeTemplate>` (exact-type, with declarative ports) are supported.
2. Cross-reference the new how-to with a `[[declare-ports-in-axaml]]` link.

- [ ] **Step 3: Update `custom-port-provider.md`**

Add a "Recommended path: AXAML" section above the existing `FixedPortProvider` code example. Frame the existing code as the override path / dynamic-construction path. Cross-reference `[[declare-ports-in-axaml]]`.

- [ ] **Step 4: Update `strategies.md`**

Add a short paragraph on `NodePortRegistry`:

```markdown
## NodePortRegistry

A static, process-wide registry mapping `Type` → `IReadOnlyList<PortSpec>`. Populated by
`<ng:NodeTemplate>` at XAML parse time; consulted by `Node.Ports` and `Node.PortProvider`
on first read.

- Registration happens during `InitializeComponent()`. Construct nodes after that point.
- Lookups are exact-type — no inheritance walk-up. See [[declare-ports-in-axaml]].
- "Code wins": any explicit `node.PortProvider = …` assignment (including `= null`) suppresses
  registry consultation for that node instance permanently.
```

- [ ] **Step 5: Commit**

```bash
git add docs/userguide/2-how-to/declare-ports-in-axaml.md \
        docs/userguide/2-how-to/custom-node-template.md \
        docs/userguide/2-how-to/custom-port-provider.md \
        docs/userguide/3-reference/strategies.md
git commit -m "docs: document declarative AXAML ports and NodePortRegistry"
```

---

## Final verification

- [ ] **Full test suite green**

Run: `dotnet test tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
Expected: all tests pass — 684 pre-existing + new tests added across Tasks 2, 3, 4, 5.

- [ ] **Build clean**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Sample apps still compile**

Run: `dotnet build samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj samples/GettingStarted/GettingStarted.csproj`
Expected: both build clean. No behavioral change required — they don't yet use `<ng:NodeTemplate>`.
