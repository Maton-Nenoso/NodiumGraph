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
- **Code wins**: if `Node.PortProvider` is already set, registry defaults do nothing.
- No model semantics change. `Port`, `PortAnchor`, `FixedPortProvider`, `Node.PortProvider` are unchanged.

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
    public static bool TryGet(Type nodeType, out IReadOnlyList<PortDefinition> definitions);
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

#### Conflict policy

Re-registration for the same `nodeType`:
- If the new definition list is **structurally identical** to the existing one → silent no-op.
- Otherwise → throw `InvalidOperationException` with both lists in the message.

Structural equality compares: list length, then per entry `Name`, `Flow`, `Edge`, `Fraction`, `Label`, `MaxConnections`, `DataType`, in order. `DataType` (`object?`) uses `EqualityComparer<object?>.Default`. This is exact for strings, `Type`, primitives, and any custom type implementing `Equals` by value. Reference-typed `DataType` values without value-equality semantics fall back to reference equality and may cause a false conflict throw if a consumer somehow registers two distinct instances — the fix is either to use a value-typed `DataType` (recommended: a `string` or `Type` token) or to register the affected node type in code instead of AXAML.

This rule is forgiving for benign duplication (the same XAML loaded twice across windows) and loud for real bugs (two declarations disagreeing on topology). `Clear()` exists for tests and any consumer that genuinely wants to swap registrations at runtime.

#### Thread safety

The registry is backed by a `ConcurrentDictionary<Type, IReadOnlyList<PortDefinition>>`. `TryGet` is lock-free. `Register` and `Clear` are atomic against concurrent readers — readers see either the pre-state or the post-state, never a torn intermediate. The conflict-policy check inside `Register` is performed under the dictionary's own write lock so two concurrent identical registrations don't race past the equality check.

Tests that mutate the registry (call `Register` or `Clear`) run inside a non-parallel xUnit collection (`[Collection("NodePortRegistry")]`) so they don't interleave with one another. Tests that only read are unaffected.

### `Node.Ports` (new), `Node.PortProvider` (lazy), and materialization

Both `Node.Ports` (new) and `Node.PortProvider` (existing) trigger lazy materialization on read. The materializer is shared and idempotent, so consumers using either the old `node.PortProvider!.Ports` pattern or the new `node.Ports` shorthand see the same behavior.

```csharp
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
    set => SetField(ref _portProvider, value);
}

private void EnsureMaterialized()
{
    if (_portProvider != null) return;
    if (!NodePortRegistry.TryGet(GetType(), out var defs)) return;

    var ports = defs.Select(d => new Port(this, d.Name, d.Flow,
                                          new PortAnchor(d.Edge, d.Fraction))
    {
        Label = d.Label,
        MaxConnections = d.MaxConnections,
        DataType = d.DataType,
    }).ToList();
    PortProvider = new FixedPortProvider(ports);   // setter raises PropertyChanged
}
```

Properties of this rule:
- **Lazy and idempotent.** First access (via either getter) materializes once; subsequent accesses return the existing provider's ports.
- **Code wins.** If a consumer has assigned `PortProvider` (in a ctor, in code-behind, anywhere) the registry is never consulted on subsequent reads.
- **Existing API surface keeps working.** Code that reads `node.PortProvider?.Ports` or subscribes to `PortProvider` changes does not need migration — it sees the materialized provider on first access. `node.Ports` is sugar over the same path.
- **No live updates.** Re-parsing AXAML or calling `NodePortRegistry.Clear()`/`Register()` after a node has materialized leaves the existing node's ports untouched. New nodes constructed afterward see the new registry state.
- **Side effect on getter is bounded.** At most one `PortProvider` assignment per node instance, gated by the `_portProvider != null` early-out.

#### Canvas-side render path

The existing canvas code already subscribes to `PortProvider` changes and renders from `PortProvider.Ports`. With the lazy getter, the first time the canvas reads `node.PortProvider` after the node enters the graph, the materializer fires, the setter raises `PropertyChanged`, and the canvas's existing change handler attaches its listeners. No new wiring needed; the canvas does not need to know about the registry.

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
- Register valid definitions → `TryGet` returns them.
- Re-register structurally identical list → no-op, no throw.
- Re-register with any difference → throws `InvalidOperationException`.
- Empty `Name` → throws at `Register`.
- Duplicate `Name` → throws at `Register`.
- Out-of-range `Fraction` → throws at `Register`.
- `TryGet` for a derived type with no entry but a registered base → returns the base's defs. (walk-up)
- `TryGet` for a derived type with its own entry → returns the derived defs, not the base's. (most-specific wins)
- `TryGet` for a type whose chain has no entries → returns false.
- `DataType` value equality: two registrations with `DataType = "x"` (string) → identical, no throw. Two registrations with `DataType = typeof(int)` → identical, no throw. Two registrations with distinct `new object()` values → throws (documents the ref-equality footgun).
- `Clear()` empties the registry; existing materialized nodes unaffected (covered in materialization tests).

**`NodeRegistryMaterializationTests.cs`** (pure model):
- Register defs for `TypeA`. Construct `new TypeA()`. Access `.Ports` → returns ports matching the defs, `PortProvider` is a `FixedPortProvider`.
- Same scenario but read `node.PortProvider` (the existing API) instead of `.Ports` → also triggers materialization; returns the new provider.
- Pre-assign `node.PortProvider = …` before any read → registry is never consulted on subsequent reads; existing provider wins.
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

Added on `NodiumGraph.Model.Node`:
- `Ports` (`IReadOnlyList<Port>`, lazy-materializing read-only property).

Behavior change on `NodiumGraph.Model.Node`:
- `PortProvider` getter now triggers the same lazy materialization as `Ports` (first read consults `NodePortRegistry` if no provider is set). Type and setter signature are unchanged.

No removals. No changes to `Port`, `PortAnchor`, `FixedPortProvider`, or any handler/strategy interface.
