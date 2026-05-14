---
title: Declarative AXAML port definitions
tags: [plan, spec]
status: active
created: 2026-05-14
updated: 2026-05-14
---

# Declarative AXAML port definitions

Today, ports are declared in C# only: a consumer instantiates `Port(node, name, flow, anchor)` and wraps them in a `FixedPortProvider` assigned to `Node.PortProvider`. The DataTemplate side of NodiumGraph already drives node *visuals* per CLR type via `<ng:NodePresenter>`, but the port topology must be wired separately in code-behind. This design lets a consumer declare a fixed port set inline in a custom data template, accessible to code immediately after `InitializeComponent()` returns.

The mental model: **port topology is a per-type fact**, expressed alongside the per-type visual template. The DataTemplate is the per-type artifact; a custom `NodeTemplate` carries the port definitions alongside the visual. A static `NodePortRegistry` carries the definitions from XAML-parse time to model-construction time so code can use them.

## Goals

- Declare a node's fixed port set in AXAML, on the same per-type artifact that themes the node body.
- Once `InitializeComponent()` returns, `new MyNode().Ports.First(p => p.Name == "out")` works without any UI realization — connections can be built in code from declared ports.
- Materialization lives on the model (`Node`), not the view. View realization is unaffected.
- **Code wins**: any explicit `Node.PortProvider = …` assignment (including `= null`) suppresses registry defaults from that point on.
- No signature changes to existing model types. `Port`, `PortAnchor`, `FixedPortProvider`, `Node.PortProvider` keep their public shape; the `Node.PortProvider` getter's behavior is extended to lazy-materialize (called out in the Public surface delta).

## Non-goals

- **Bindings on port fields.** `PortDefinition` is literal-only. `Edge`/`Fraction` carry anchor identity — binding-driven changes would force port recreation and detach live connections. Static covers the realistic use case.
- **Runtime port mutation through the AXAML pipeline.** Add/remove/relabel after materialization stays imperative against the model. Re-parsing or hot-reloading the XAML after a node has materialized its ports does **not** rewrite the model.
- **`DynamicPortProvider` in AXAML.** Its purpose is imperative ("create a port at the hit point on drag"). Setting a `DynamicPortProvider` from code remains the answer.

## Design

### `PortDefinition`

Lightweight POCO. Lives in `NodiumGraph.Controls` because it's strictly a XAML-side construction recipe and carries no model state.

```csharp
namespace NodiumGraph.Controls;

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

- Two attributes for the anchor (`Edge`, `Fraction`) — matches `PortAnchor(Edge, Fraction)` one-for-one and gives Avalonia XAML completion for the `PortEdge` enum values. No type converter; a converter is a clean later addition if terseness demands it.
- All other fields map directly to `Port` properties or constructor arguments.

### `NodeTemplate`

A new `IDataTemplate` implementation that pairs port definitions with the visual template for a given node CLR type.

```csharp
namespace NodiumGraph.Controls;

public sealed class NodeTemplate : IDataTemplate, ISupportInitialize
{
    public Type? DataType { get; set; }

    public IList<PortDefinition> Ports { get; } = new List<PortDefinition>();

    [Content]
    [TemplateContent(TemplateResultType = typeof(Control))]
    public object? Content { get; set; }       // the visual tree (e.g. <ng:NodePresenter>...)

    public bool Match(object? data) => data != null && DataType == data.GetType();
    public Control? Build(object? param) => TemplateContent.Load<Control>(Content)?.Result;

    void ISupportInitialize.BeginInit() { }
    void ISupportInitialize.EndInit()
    {
        if (DataType is null) return;       // unusable as a template; nothing to register
        if (Ports.Count == 0) return;       // visual-only: behaves like a plain <DataTemplate>
        NodePortRegistry.Register(DataType, Ports);
    }
}
```

XAML usage:

```xml
<Window.DataTemplates>
  <ng:NodeTemplate DataType="local:InputSourceNode">
    <ng:NodeTemplate.Ports>
      <ng:PortDefinition Name="out" Flow="Output" Edge="Right" Fraction="0.5"/>
    </ng:NodeTemplate.Ports>

    <ng:NodePresenter HeaderBackground="#10B981">
      <TextBlock Text="Reads data..." />
    </ng:NodePresenter>
  </ng:NodeTemplate>
</Window.DataTemplates>
```

Notes:
- `NodeTemplate` is a drop-in replacement for `DataTemplate` for any node type that wants AXAML-declared ports. Existing `<DataTemplate DataType="...">` declarations keep working — they just don't carry port metadata.
- The visual is the `[Content]` slot, so the XAML reads naturally with the visual inline; `Ports` lives in a property element.
- `Match` uses **exact type matching** (`DataType == data.GetType()`), not Avalonia's default `IsInstanceOfType`. This is deliberate: it aligns the visual selection with the registry's exact-type lookup so port topology and visual never diverge based on Avalonia's template ordering. Consumers who want a single shared visual across a hierarchy keep using plain `<DataTemplate>` (and wire ports in code) — `<ng:NodeTemplate>` is the opt-in exact-type surface.
- A `<ng:NodeTemplate>` with no `<NodeTemplate.Ports>` element (or with an empty list) does **not** register — `EnsureMaterialized` then leaves `PortProvider` null for that type, same as if no template existed.
- The exact XAML hook for registration is `ISupportInitialize.EndInit`, which Avalonia invokes after all properties are set during XAML load. If implementation discovers EndInit isn't reliably invoked on nested elements, fall back to registering on first `Match`/`Build` call.

### `NodePortRegistry`

Static, process-wide. Populated by `NodeTemplate` during XAML parse; consulted by `Node` on first port access.

```csharp
namespace NodiumGraph;

public static class NodePortRegistry
{
    public static void Register(NodeTemplate template);
    public static void Register(Type nodeType, IEnumerable<PortDefinition> definitions);
    public static bool TryGet(Type nodeType, out IReadOnlyList<PortSpec> snapshot);
    public static void Clear();   // for tests / hot-reload scenarios
}
```

#### Lookup: exact type only

`TryGet(t)` does a single `_store.TryGetValue(t, …)` — exact-type match, no walk-up. This pairs with `NodeTemplate.Match` (also exact-type) so the visual selection and the port topology are picked by the same predicate. Two consequences:

- A `<ng:NodeTemplate>` registered for a base type does **not** apply to derived types. Each concrete type that needs declared ports gets its own `<ng:NodeTemplate>`.
- Consumers who want a shared visual across a hierarchy use a plain `<DataTemplate>` (which keeps `IsInstanceOfType` matching) and wire ports in code, or declare one `<ng:NodeTemplate>` per concrete type with the same `<NodeTemplate.Content>`.

This is intentional: trying to reconcile registry walk-up with Avalonia's order-dependent template selection leaves visual/port pairs that can drift apart based on declaration order. Exact-type-everywhere is the only consistent answer that doesn't introduce ordering footguns.

#### Validation at `Register` time

- `nodeType` must be non-null and assignable to `Node`.
- Each `PortDefinition.Name` must be non-null and non-empty.
- All `Name` values must be unique within the registered list.
- Each `Fraction` must be in `[0, 1]`; each `Edge` must be a defined `PortEdge` value. (Already enforced by `PortAnchor`, but surfaced at registration so structural AXAML errors fail at parse time, not lazily on first node construction.)
- Each `DataType` must be `null`, a `string`, a `System.Type`, or a value type. Reference-typed `DataType` values (other than `string`/`Type`) are rejected because the registry stores them in shallow snapshots — mutation post-registration could change conflict-check outcomes or what a future materialization sees. Consumers who need a class-typed `DataType` token bypass the registry entirely and assign `Node.PortProvider` in code.

#### Immutable snapshot at register time

The registry stores immutable snapshots of each registered definition list, not references to `PortDefinition` instances. The snapshot element type is public so consumers can introspect the registry directly:

```csharp
namespace NodiumGraph;

public readonly record struct PortSpec(
    string Name, PortFlow Flow, PortEdge Edge, double Fraction,
    string? Label, uint? MaxConnections, object? DataType);
```

`Register(Type, IEnumerable<PortDefinition>)` validates, then projects each `PortDefinition` into a `PortSpec` and stores the result as a `ReadOnlyCollection<PortSpec>` (or `ImmutableArray<PortSpec>`) over a private backing array. Returning a non-downcastable wrapper is deliberate: an `IReadOnlyList<PortSpec>` exposed by `TryGet` cannot be cast to `List<PortSpec>` to mutate the registry's view through the back door. Subsequent mutations to the original `PortDefinition` instances or to the `NodeTemplate.Ports` collection have no effect on the registry either — projection takes a value copy.

The snapshot is shallow only at `DataType` (which is `object?`). The validation rule above limits `DataType` to `null`/`string`/`Type`/value-typed values, all of which are themselves immutable or value-copied — so a registered `PortSpec` is effectively fully immutable in practice.

#### Conflict policy

Re-registration for the same `nodeType`:
- If the new definition list is **structurally identical** to the existing one → silent no-op.
- Otherwise → throw `InvalidOperationException` with both lists in the message.

Structural equality compares: list length, then per entry `Name`, `Flow`, `Edge`, `Fraction`, `Label`, `MaxConnections`, `DataType`, in order. `DataType` uses `EqualityComparer<object?>.Default`. Because `DataType` is restricted at register time to `null`/`string`/`Type`/value-typed values (see the Validation section), equality is well-defined: strings, `Type`, and value types all have correct `Equals` semantics. The reference-equality footgun for arbitrary class instances is moot — those are rejected at registration.

This rule is forgiving for benign duplication (the same XAML loaded twice across windows) and loud for real bugs (two declarations disagreeing on topology). `Clear()` exists for tests and any consumer that genuinely wants to swap registrations at runtime.

#### Thread safety

Concrete algorithm:

```csharp
private static readonly ConcurrentDictionary<Type, IReadOnlyList<PortSpec>> _store = new();
private static readonly object _writeLock = new();

public static void Register(Type t, IEnumerable<PortDefinition> defs)
{
    var snapshot = ValidateAndSnapshot(t, defs);  // throws on validation failure
    lock (_writeLock)
    {
        if (_store.TryGetValue(t, out var existing))
        {
            if (!StructurallyEqual(existing, snapshot)) throw new InvalidOperationException(/* both lists */);
            return;                                         // identical → no-op
        }
        _store[t] = snapshot;
    }
}

public static void Clear()
{
    lock (_writeLock) _store.Clear();
}

public static bool TryGet(Type t, out IReadOnlyList<PortSpec> snapshot)
{
    if (_store.TryGetValue(t, out snapshot)) return true;
    snapshot = Array.Empty<PortSpec>();
    return false;
}
```

`TryGet` is lock-free (single-key reads on `ConcurrentDictionary` are atomic and lock-free) and exact-type — no walk-up, consistent with `NodeTemplate.Match`. `Register` and `Clear` serialize through `_writeLock` so the "read existing → compare → throw or accept" sequence is atomic against other writers. Readers see either the pre-state or the post-state on any given key, never a torn intermediate.

Storage immutability: the stored snapshots are wrapped in `ReadOnlyCollection<PortSpec>` (or returned as `ImmutableArray<PortSpec>`) over a private backing array, so a consumer cannot downcast `IReadOnlyList<PortSpec>` to `List<PortSpec>` and mutate the registry's view through the back door.

Any test that **observes** registry state — whether reading or writing — runs inside the non-parallel xUnit collection `[Collection("NodePortRegistry")]`. Read-only tests that depend on a specific registry state would otherwise race against another test's `Clear()` or `Register()`. Tests that touch the registry only as a side-effect of unrelated code (and don't assert on its contents) are unaffected.

### `Node.Ports` (new), `Node.PortProvider` (lazy), and materialization

Both `Node.Ports` (new) and `Node.PortProvider` (existing) trigger lazy materialization on read. A `_portProviderExplicit` sentinel records that the consumer has touched the setter at any point — including `= null` — so explicit assignment fully suppresses registry consultation thereafter. The materializer is shared and idempotent.

```csharp
private bool _portProviderExplicit;

public IReadOnlyList<Port> Ports
{
    get
    {
        EnsureMaterialized();
        return _portProvider?.Ports ?? Array.Empty<Port>();
    }
}

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

private void EnsureMaterialized()
{
    if (_portProviderExplicit) return;                       // code wins, including explicit null
    if (!NodePortRegistry.TryGet(GetType(), out var defs)) return;

    var ports = defs.Select(d => new Port(this, d.Name, d.Flow,
                                          new PortAnchor(d.Edge, d.Fraction))
    {
        Label = d.Label,
        MaxConnections = d.MaxConnections,
        DataType = d.DataType,
    }).ToList();
    PortProvider = new FixedPortProvider(ports);             // goes through setter, sets flag, raises PropertyChanged
}
```

Properties of this rule:
- **Lazy and idempotent.** First access (via either getter) materializes once; the sentinel flips to true and subsequent accesses early-out.
- **Code wins, including explicit null.** Any setter call — `node.PortProvider = customProvider` or `node.PortProvider = null` — flips the sentinel, suppressing the registry from that point on. `= null` is now a permanent "this node has no ports," not a temporary clear that the registry will undo on the next read.
- **Existing API surface keeps working.** Code that reads `node.PortProvider?.Ports` or subscribes to `PortProvider` changes does not need migration — it sees the materialized provider on first access. `node.Ports` is sugar over the same path.
- **No live updates.** Re-parsing AXAML or calling `NodePortRegistry.Clear()`/`Register()` after a node has materialized leaves the existing node's ports untouched. The sentinel is true after auto-materialization, so subsequent reads never reconsult the registry.
- **Side effect on getter is bounded.** At most one `PortProvider` assignment per node instance, gated by the sentinel.

#### Canvas-side render path — required changes

The lazy getter interacts badly with the current order in `NodiumGraphCanvas.AddNodeContainer` (lines 1753–1780). Today it does:

```csharp
node.PropertyChanged += OnNodePropertyChanged;        // (1) subscribe
if (node.PortProvider != null)                         // (2) read — would now materialize
    AttachProvider(node, node.PortProvider);           // (3) attach
```

With the lazy getter, step (2) materializes via the setter, which raises `PropertyChanged`. The subscription from step (1) routes that through `OnNodePropertyChanged`, which calls `AttachProvider`. Then step (3) calls `AttachProvider` again — port subscriptions and provider lambdas double up, `_providerAddedHandlers` overwrites its own first registration while leaving the orphaned lambda subscribed.

Two changes fix this:

1. **Reorder `AddNodeContainer` so the read happens before the subscription:**

```csharp
var provider = node.PortProvider;                      // may auto-materialize; no subscriber yet
node.PropertyChanged += OnNodePropertyChanged;
if (provider != null)
    AttachProvider(node, provider);
```

The `PropertyChanged` raised by auto-materialization passes with no subscriber. The subsequent explicit `AttachProvider` call is the only one.

2. **Make `AttachProvider` idempotent** (defense in depth):

```csharp
private void AttachProvider(Node node, IPortProvider provider)
{
    if (_nodeProviders.TryGetValue(node, out var existing) && existing == provider)
        return;                                        // already attached
    // ... existing body ...
}
```

This guards against any other code path that ends up reading `node.PortProvider` while a `PropertyChanged` subscription is live (extensions, headless test harnesses, future callers).

No other canvas changes are needed.

### Registration timing — the rule, plainly

The materializer runs at **first `Ports`/`PortProvider` access**, not at construction. What the registry contains at that moment is what the node gets. Construction order relative to XAML parse is therefore irrelevant — the node's port topology is determined by the registry state at first read.

| Sequence | Outcome for that node |
|----------|----------------------|
| Access at time T; registry has entry for the node's type at T | Materializer fires once; node holds the registered ports thereafter. |
| Access at time T; registry has **no** entry at T | Materializer no-ops, provider stays null. A subsequent access re-checks the registry — late registration **is** picked up on the next access. Nothing is cached on the miss side. |
| `PortProvider` assigned in code at any point | Subsequent accesses early-out; the registry is never consulted again for that node. Code wins. |
| `NodePortRegistry.Clear()` then new node constructed and accessed | New node sees an empty registry; materializer no-ops (and a later `Register` will be picked up on a later access, as above). |
| `NodePortRegistry.Clear()` while an existing node already has a materialized provider | Existing node keeps its provider. The clear has no effect on already-materialized nodes. |
| Hot-reload swapping XAML | The conflict policy throws if a re-parsed `NodeTemplate` registers a different list for an already-registered type. Hot-reload tooling must call `NodePortRegistry.Clear()` (or the targeted Remove call, if added later) before re-parsing the affected window. After `Clear()`, new nodes use the new registration; existing nodes keep their already-materialized providers. |

The practical takeaway for app authors: construct nodes after `InitializeComponent()` returns and the rule is simply "AXAML declares, code uses." For tests and code-only paths, the registry stays empty and ports are wired imperatively, as today.

## Open question resolutions

| Q | Resolution | Reason |
|---|------------|--------|
| Anchor syntax | Two attributes (`Edge`, `Fraction`) | Discoverability; mirrors `PortAnchor` record shape; converter is a clean later addition. |
| Materialization point | Model, lazy on both `Node.Ports` and `Node.PortProvider` getters | Existing `node.PortProvider!.Ports` pattern keeps working; topology is a model concern; lazy hides the order-of-operations question for the common path. |
| Conflict policy | Identical → no-op, different → throw | Forgiving for benign duplication, loud for real bugs. `DataType` is restricted to `null`/`string`/`Type`/value types at registration, so structural equality is well-defined. |
| Bindings on `PortDefinition` | POCO, literal-only | Anchor identity isn't safely mutable; promoting to `AvaloniaObject` later is source-compatible in the intended literal-attribute usage. |
| `DynamicPortProvider` in AXAML | Punt | Imperative by nature; trivial to add later. |
| Type-hierarchy lookup | Exact type only, in both `NodeTemplate.Match` and `NodePortRegistry.TryGet` | Walk-up + `IsInstanceOfType` can't be reconciled with Avalonia's order-dependent template selection — visual/ports would drift. Exact-type-everywhere is the only consistent answer. Shared-visual hierarchies use plain `<DataTemplate>` + code-side ports. |
| `DataType` allowed values in registry | `null`, `string`, `Type`, or any value type | Snapshot is shallow at `DataType`; the restriction makes the snapshot effectively immutable in practice. Class-typed tokens require code-side `PortProvider` assignment that bypasses the registry. |
| Name uniqueness | Enforced at `Register` time (declarative path); `FixedPortProvider` unchanged | Name-based lookup is the consumer's primary use case for declared ports; silent duplicates are a footgun. |
| Thread safety | `ConcurrentDictionary` store, lock-free reads, atomic Register/Clear; tests in non-parallel xUnit collection | Static state + parallel xUnit needs an explicit contract. |

## Testing

xUnit v3 + Avalonia headless. Three test files:

All tests that mutate the registry live in xUnit collection `[Collection("NodePortRegistry")]` to serialize against each other. Each such test starts with `NodePortRegistry.Clear()`.

**`NodePortRegistryTests.cs`** (pure model, no Avalonia):
- Register valid definitions → `TryGet` returns a matching `IReadOnlyList<PortSpec>`.
- Re-register structurally identical list → no-op, no throw.
- Re-register with any difference → throws `InvalidOperationException`.
- Empty `Name` → throws at `Register`.
- Duplicate `Name` → throws at `Register`.
- Out-of-range `Fraction` → throws at `Register`.
- `TryGet` for a registered exact type → returns the snapshot.
- `TryGet` for a derived type with no entry, even when its base is registered → returns false. (exact-type only; no walk-up)
- `TryGet` for an unregistered type → returns false.
- `DataType` validation: registering with `null`, `"x"` (string), `typeof(int)`, an enum value, or a primitive → succeeds. Registering with a `new object()`, a custom class instance, or any non-`string`/`Type` reference type → throws `ArgumentException` at `Register`.
- `DataType` value equality (within the allowed set): two registrations with `DataType = "x"` → identical, no throw. Two registrations with `DataType = typeof(int)` → identical, no throw. Two registrations with the same enum value → identical, no throw.
- **Snapshot immutability:** register a `PortDefinition`, then mutate its `Name`/`Fraction`/etc. after the fact and re-call `TryGet` → returned snapshot reflects the original values, not the mutated ones. Also: mutate or clear the original `IList<PortDefinition>` after register → registry state is unaffected.
- **Snapshot non-downcastable:** call `TryGet` → assert the returned `IReadOnlyList<PortSpec>` is **not** castable to `List<PortSpec>` (or, if cast succeeds via some adapter, that the cast target is itself read-only and `Add`/`Clear` throw). Guards against mutating the registry's internal storage through `IReadOnlyList`.
- **Concurrent Register:** two threads racing `Register` for the same type with different defs → one wins, the other throws `InvalidOperationException`. Two threads with identical defs → both succeed (one inserts, one no-ops).
- `Clear()` empties the registry; existing materialized nodes unaffected (covered in materialization tests).

**`NodeRegistryMaterializationTests.cs`** (pure model):
- Register defs for `TypeA`. Construct `new TypeA()`. Access `.Ports` → returns ports matching the defs, `PortProvider` is a `FixedPortProvider`.
- Same scenario but read `node.PortProvider` (the existing API) instead of `.Ports` → also triggers materialization; returns the new provider.
- Pre-assign `node.PortProvider = customProvider` before any read → registry never consulted; `customProvider` wins.
- **Explicit null suppresses registry:** register defs for `TypeA`, construct `new TypeA()`, assign `node.PortProvider = null`, then access `.Ports` → returns empty, `PortProvider` stays null. The sentinel makes `= null` a permanent choice.
- No registration for `TypeB`. Construct `new TypeB()`. Access `.Ports` → returns empty; `PortProvider` stays null. Then `Register(typeof(TypeB), …)`; access `.Ports` again → late registration takes effect on this access.
- Optional fields (`Label`, `MaxConnections`, `DataType`) propagate to the materialized `Port`.
- Repeated `.Ports` access returns the same list reference; no re-materialization.
- `Node`s of two different registered types each materialize independently from their own registry entries.
- Materialization fires `PropertyChanged(nameof(PortProvider))` exactly once; subscribers attached before first access see the change event.
- After `Clear()` and re-`Register()` with different defs, a previously-materialized node still has its original provider (no live update); a newly-constructed node of the same type gets the new defs.

**`DeclarativeNodeTemplateTests.cs`** (Avalonia headless):
- Load a `Window` whose XAML has `<ng:NodeTemplate>` with `<ng:NodeTemplate.Ports>` → after `InitializeComponent`, registry contains the entries.
- Construct a node of the templated type → `.Ports` is populated.
- Bind a graph to a canvas in that window; node renders with the declared ports (canvas-level integration: the canvas attaches the materialized provider, ports hit-test, drag-to-connect resolves to the declared ports).
- **No canvas double-attach:** add a node whose registry entry causes lazy materialization during `AddNodeContainer`; assert `provider.PortAdded` has exactly one subscriber after attach (counts handlers via the `_providerAddedHandlers` snapshot or via `Delegate.GetInvocationList()` on a probe).
- **Visual-only `<ng:NodeTemplate>` (no `<NodeTemplate.Ports>` element):** load → registry has no entry for that type, node's `PortProvider` stays null, the visual still renders.
- **Empty `<NodeTemplate.Ports>` element:** loaded the same as visual-only — no registration.
- **Exact-type Match:** declare `<ng:NodeTemplate DataType="local:BaseNode">` only; render a `local:DerivedNode` → Avalonia does NOT pick this template (`Match` returns false); a fallback `<DataTemplate>` or `DefaultTemplates.NodeTemplate` renders the derived instance instead.
- Invalid `PortDefinition` (bad `Fraction`) in XAML → window load surfaces the exception cleanly.
- Invalid `DataType` (a class instance) in XAML → window load surfaces the exception cleanly.

## Documentation impact

- New how-to: `docs/userguide/2-how-to/declare-ports-in-axaml.md`. Covers the `<ng:NodeTemplate>` form, the registration-timing rule (after `InitializeComponent`), and one end-to-end example with a code-side `Connection` referencing AXAML-declared ports by name.
- Update `docs/userguide/2-how-to/custom-node-template.md` to cross-reference the new how-to and to clarify that `<DataTemplate>` and `<ng:NodeTemplate>` are both supported; the latter adds port metadata.
- Update `docs/userguide/2-how-to/custom-port-provider.md` to note that for fixed port sets, AXAML declaration via `<ng:NodeTemplate>` is now the recommended path; the code-side `FixedPortProvider` example remains as the override / dynamic-construction path.
- Update `docs/userguide/3-reference/strategies.md` with a short paragraph on `NodePortRegistry` and the registration-timing rule.

## Out of scope / future

- Markup-extension form for terse anchors (`Anchor="Left:0.5"`) — additive.
- `PortDefinition : AvaloniaObject` with `StyledProperty` for `Label`/`DataType` to allow bindings — additive.
- A `<ng:NodeTemplate.PortProvider>` content slot to declare any `IPortProvider` (including `DynamicPortProvider`) — additive.
- Type-hierarchy lookup (walk-up registry + `IsInstanceOfType` Match) — additive, but only if a clear story emerges for keeping visual selection and port topology aligned under Avalonia's order-dependent template walk. Not pursued in v1.
- Source-generator-backed registration (parse XAML at build time, emit static registration) — eliminates the runtime `EndInit` dependency; not needed for v1.

## Public surface delta

Added in `NodiumGraph.Controls`:
- `PortDefinition` (POCO).
- `NodeTemplate` (`IDataTemplate, ISupportInitialize`).

Added in `NodiumGraph`:
- `NodePortRegistry` (static).
- `PortSpec` (immutable record; the registry's snapshot element type).

Added on `NodiumGraph.Model.Node`:
- `Ports` (`IReadOnlyList<Port>`, lazy-materializing read-only property).

Behavior change on `NodiumGraph.Model.Node`:
- `PortProvider` getter triggers lazy materialization (first read consults `NodePortRegistry` if the setter has never been called). Setter signature unchanged; setter now also flips a private sentinel so explicit assignment (including `= null`) permanently suppresses registry consultation.

Internal change in `NodiumGraph.Controls.NodiumGraphCanvas`:
- `AddNodeContainer` reorders to read `PortProvider` before subscribing to `PropertyChanged`.
- `AttachProvider` short-circuits if `_nodeProviders` already holds the same provider for the node.

No removals. No changes to `Port`, `PortAnchor`, `FixedPortProvider`, or any handler/strategy interface.
