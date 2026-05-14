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

    public bool Match(object? data) => DataType?.IsInstanceOfType(data) ?? false;
    public Control? Build(object? param) => TemplateContent.Load<Control>(Content)?.Result;

    void ISupportInitialize.BeginInit() { }
    void ISupportInitialize.EndInit() => NodePortRegistry.Register(this);
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

#### Lookup: most-specific match wins

`TryGet(t)` walks `t`, `t.BaseType`, `t.BaseType.BaseType`, … until either a registered entry is found or the chain bottoms out. The first hit wins. This means:

- Registering `<ng:NodeTemplate DataType="local:AbstractDataNode">` applies its ports to every derived type that doesn't have its own registration.
- A more specific registration on a derived type fully replaces (does not merge) the base type's ports for that derived type.
- This mirrors Avalonia's `NodeTemplate.Match` semantics (`DataType?.IsInstanceOfType(data)`), so the visual and the ports stay consistent across the type hierarchy.

#### Validation at `Register` time

- `nodeType` must be non-null and assignable to `Node`.
- Each `PortDefinition.Name` must be non-null and non-empty.
- All `Name` values must be unique within the registered list.
- Each `Fraction` must be in `[0, 1]`; each `Edge` must be a defined `PortEdge` value. (Already enforced by `PortAnchor`, but surfaced at registration so structural AXAML errors fail at parse time, not lazily on first node construction.)

#### Immutable snapshot at register time

The registry stores immutable snapshots of each registered definition list, not references to `PortDefinition` instances. Internally:

```csharp
internal readonly record struct PortSpec(
    string Name, PortFlow Flow, PortEdge Edge, double Fraction,
    string? Label, uint? MaxConnections, object? DataType);
```

`Register(Type, IEnumerable<PortDefinition>)` validates, then projects each `PortDefinition` into a `PortSpec` and stores an `IReadOnlyList<PortSpec>`. Subsequent mutations to the original `PortDefinition` instances or to the `NodeTemplate.Ports` collection do not affect the registry. `TryGet` returns the snapshot.

#### Conflict policy

Re-registration for the same `nodeType`:
- If the new definition list is **structurally identical** to the existing one → silent no-op.
- Otherwise → throw `InvalidOperationException` with both lists in the message.

Structural equality compares: list length, then per entry `Name`, `Flow`, `Edge`, `Fraction`, `Label`, `MaxConnections`, `DataType`, in order. `DataType` (`object?`) uses `EqualityComparer<object?>.Default`. This is exact for strings, `Type`, primitives, and any custom type implementing `Equals` by value. Reference-typed `DataType` values without value-equality semantics fall back to reference equality and may cause a false conflict throw if a consumer somehow registers two distinct instances — the fix is either to use a value-typed `DataType` (recommended: a `string` or `Type` token) or to register the affected node type in code instead of AXAML.

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
    for (Type? cur = t; cur != null; cur = cur.BaseType)
        if (_store.TryGetValue(cur, out snapshot)) return true;
    snapshot = Array.Empty<PortSpec>();
    return false;
}
```

`TryGet` is lock-free (single-key reads on `ConcurrentDictionary` are atomic and lock-free; the walk-up is a sequence of independent lock-free reads). `Register` and `Clear` serialize through `_writeLock` so the "read existing → compare → throw or accept" sequence is atomic against other writers. Readers see either the pre-state or the post-state on any given key, never a torn intermediate.

Tests that mutate the registry (call `Register` or `Clear`) run inside a non-parallel xUnit collection (`[Collection("NodePortRegistry")]`) so they don't interleave with one another. Tests that only read are unaffected.

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
| Hot-reload swapping XAML | New nodes use the new registration; existing nodes are unaffected. Consumers who want a refresh re-create the affected nodes. |

The practical takeaway for app authors: construct nodes after `InitializeComponent()` returns and the rule is simply "AXAML declares, code uses." For tests and code-only paths, the registry stays empty and ports are wired imperatively, as today.

## Open question resolutions

| Q | Resolution | Reason |
|---|------------|--------|
| Anchor syntax | Two attributes (`Edge`, `Fraction`) | Discoverability; mirrors `PortAnchor` record shape; converter is a clean later addition. |
| Materialization point | Model, lazy on both `Node.Ports` and `Node.PortProvider` getters | Existing `node.PortProvider!.Ports` pattern keeps working; topology is a model concern; lazy hides the order-of-operations question for the common path. |
| Conflict policy | Identical → no-op, different → throw | Forgiving for benign duplication, loud for real bugs. `DataType` equality uses `EqualityComparer<object?>.Default`; reference-typed `DataType` may false-throw — recommend string/Type tokens. |
| Bindings on `PortDefinition` | POCO, literal-only | Anchor identity isn't safely mutable; promoting to `AvaloniaObject` later is source-compatible in the intended literal-attribute usage. |
| `DynamicPortProvider` in AXAML | Punt | Imperative by nature; trivial to add later. |
| Type-hierarchy lookup | Walk up `Type.BaseType`, most-specific match wins | Matches `NodeTemplate.Match` semantics (`IsInstanceOfType`); consistent visual + ports inheritance; a derived registration fully replaces the base's ports (no merging). |
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
- `TryGet` for a derived type with no entry but a registered base → returns the base's snapshot. (walk-up)
- `TryGet` for a derived type with its own entry → returns the derived snapshot, not the base's. (most-specific wins)
- `TryGet` for a type whose chain has no entries → returns false.
- `DataType` value equality: two registrations with `DataType = "x"` (string) → identical, no throw. Two registrations with `DataType = typeof(int)` → identical, no throw. Two registrations with distinct `new object()` values → throws (documents the ref-equality footgun).
- **Snapshot immutability:** register a `PortDefinition`, then mutate its `Name`/`Fraction`/etc. after the fact and re-call `TryGet` → returned snapshot reflects the original values, not the mutated ones. Also: mutate or clear the original `IList<PortDefinition>` after register → registry state is unaffected.
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
- Invalid `PortDefinition` (bad `Fraction`) in XAML → window load surfaces the exception cleanly.

## Documentation impact

- New how-to: `docs/userguide/2-how-to/declare-ports-in-axaml.md`. Covers the `<ng:NodeTemplate>` form, the registration-timing rule (after `InitializeComponent`), and one end-to-end example with a code-side `Connection` referencing AXAML-declared ports by name.
- Update `docs/userguide/2-how-to/custom-node-template.md` to cross-reference the new how-to and to clarify that `<DataTemplate>` and `<ng:NodeTemplate>` are both supported; the latter adds port metadata.
- Update `docs/userguide/2-how-to/custom-port-provider.md` to note that for fixed port sets, AXAML declaration via `<ng:NodeTemplate>` is now the recommended path; the code-side `FixedPortProvider` example remains as the override / dynamic-construction path.
- Update `docs/userguide/3-reference/strategies.md` with a short paragraph on `NodePortRegistry` and the registration-timing rule.

## Out of scope / future

- Markup-extension form for terse anchors (`Anchor="Left:0.5"`) — additive.
- `PortDefinition : AvaloniaObject` with `StyledProperty` for `Label`/`DataType` to allow bindings — additive.
- A `<ng:NodeTemplate.PortProvider>` content slot to declare any `IPortProvider` (including `DynamicPortProvider`) — additive.
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
