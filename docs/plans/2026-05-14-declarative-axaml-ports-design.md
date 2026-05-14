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
- **Type-hierarchy lookup.** The registry is keyed by exact CLR type. A subclass does **not** inherit a base type's registered ports; if you want inherited ports, register them on the subclass as well.
- **`DynamicPortProvider` in AXAML.** Its purpose is imperative ("create a port at the hit point on drag"). Setting a `DynamicPortProvider` from code remains the answer.
- **Pre-`InitializeComponent` access.** Code that constructs a node before its `NodeTemplate` is parsed sees an empty registry and gets no auto-materialization. That code wires ports imperatively, as today.

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

Validation at `Register` time (applies to both overloads):
- `nodeType` must be non-null and assignable to `Node`.
- Each `PortDefinition.Name` must be non-null and non-empty.
- All `Name` values must be unique within the registered list.
- Each `Fraction` must be in `[0, 1]`; each `Edge` must be a defined `PortEdge` value. (Already enforced by `PortAnchor`, but surfaced at registration so structural AXAML errors fail at parse time, not lazily on first node construction.)

Conflict policy (re-registration for the same `nodeType`):
- If the new definition list is **structurally identical** (same length, same `Name`/`Flow`/`Edge`/`Fraction`/`Label`/`MaxConnections`/`DataType` per entry, same order) → silent no-op.
- Otherwise → throw `InvalidOperationException` with both lists in the message.

This rule is forgiving for benign duplication (the same XAML loaded twice across windows) and loud for real bugs (two declarations disagreeing on topology). `Clear()` exists for tests and any consumer that genuinely wants to swap registrations at runtime.

### `Node.Ports` (new) and materialization

A new convenience property on `Node` that gives code consumers direct access to the port list and triggers lazy materialization from the registry.

```csharp
public IReadOnlyList<Port> Ports
{
    get
    {
        if (_portProvider == null && NodePortRegistry.TryGet(GetType(), out var defs))
        {
            var ports = defs.Select(d => new Port(this, d.Name, d.Flow,
                                                  new PortAnchor(d.Edge, d.Fraction))
            {
                Label = d.Label,
                MaxConnections = d.MaxConnections,
                DataType = d.DataType,
            }).ToList();
            PortProvider = new FixedPortProvider(ports);   // setter raises notify
        }
        return _portProvider?.Ports ?? Array.Empty<Port>();
    }
}
```

Properties of this rule:
- **Lazy and idempotent.** First access materializes once; subsequent accesses return the existing `PortProvider.Ports`.
- **Code wins.** If a consumer has assigned `PortProvider` (in a ctor, in code-behind, anywhere), the registry is never consulted.
- **No live updates.** Re-parsing AXAML or calling `NodePortRegistry.Clear()`/`Register()` after a node has materialized leaves the existing node's ports untouched. New nodes constructed afterward see the new registry state.
- **Side effect on getter is bounded.** At most one PortProvider assignment per node instance.

#### Canvas-side materialization trigger

To keep view rendering deterministic, the canvas also touches `node.Ports` when a node enters the graph. In `NodiumGraphCanvas.OnNodesCollectionChanged` (or its existing `AttachProvider` path), the canvas calls `_ = node.Ports;` before subscribing to `PortProvider` changes. This guarantees the provider exists (if the registry has an entry) before the first port-render frame, without coupling the canvas to the registry directly.

### Registration timing — the rule, plainly

| When | What's true |
|------|-------------|
| Before `InitializeComponent()` returns | Registry is empty unless populated manually. Nodes constructed here get no auto-materialization. |
| After `InitializeComponent()` returns | All `NodeTemplate`s in the parsed XAML tree have registered. Nodes constructed from here on auto-materialize on first `Ports` access. |
| After `NodePortRegistry.Clear()` | New nodes get nothing; existing nodes keep their already-materialized providers. |
| Hot-reload swapping XAML | New nodes use the new registration; existing nodes are unaffected. Consumers who want to force a refresh re-create the affected nodes. |

## Open question resolutions

| Q | Resolution | Reason |
|---|------------|--------|
| Anchor syntax | Two attributes (`Edge`, `Fraction`) | Discoverability; mirrors `PortAnchor` record shape; converter is a clean later addition. |
| Materialization point | Model, lazy on `Node.Ports` | Topology is a model concern; lazy hides the order-of-operations question for the common path. |
| Conflict policy | Identical → no-op, different → throw | Forgiving for benign duplication, loud for real bugs. Quoted plainly in error message. |
| Bindings on `PortDefinition` | POCO, literal-only | Anchor identity isn't safely mutable; promoting to `AvaloniaObject` later is source-compatible in the intended literal-attribute usage. |
| `DynamicPortProvider` in AXAML | Punt | Imperative by nature; trivial to add later. |
| Type-hierarchy lookup | Exact type only | Predictable. Inheritance is explicit re-registration. |
| Name uniqueness | Enforced at `Register` time (declarative path); `FixedPortProvider` unchanged | Name-based lookup is the consumer's primary use case for declared ports; silent duplicates are a footgun. |

## Testing

xUnit v3 + Avalonia headless. Three test files:

**`NodePortRegistryTests.cs`** (pure model, no Avalonia):
- Register valid definitions → `TryGet` returns them.
- Re-register structurally identical list → no-op, no throw.
- Re-register with any difference → throws `InvalidOperationException`.
- Empty `Name` → throws at `Register`.
- Duplicate `Name` → throws at `Register`.
- Out-of-range `Fraction` → throws at `Register`.
- `Clear()` empties the registry; existing materialized nodes unaffected.

**`NodeRegistryMaterializationTests.cs`** (pure model):
- Register defs for `TypeA`. Construct `new TypeA()`. Access `.Ports` → returns ports matching the defs, `PortProvider` is a `FixedPortProvider`.
- Same as above but pre-assign `node.PortProvider = …` before accessing `.Ports` → registry is never consulted; existing provider wins.
- No registration for `TypeB`. Construct `new TypeB()`. Access `.Ports` → returns empty; `PortProvider` stays null.
- Optional fields (`Label`, `MaxConnections`, `DataType`) propagate to the materialized `Port`.
- Repeated `.Ports` access returns the same list reference; no re-materialization.
- `Node` of two different types each materialize independently from their own registry entries.

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
- Type-hierarchy lookup in `NodePortRegistry` — additive if consumer demand emerges.
- Source-generator-backed registration (parse XAML at build time, emit static registration) — eliminates the runtime `EndInit` dependency; not needed for v1.

## Public surface delta

Added in `NodiumGraph.Controls`:
- `PortDefinition` (POCO).
- `NodeTemplate` (`IDataTemplate, ISupportInitialize`).

Added in `NodiumGraph`:
- `NodePortRegistry` (static).

Added on `NodiumGraph.Model.Node`:
- `Ports` (`IReadOnlyList<Port>`, lazy-materializing read-only property).

No removals. No changes to `Port`, `PortAnchor`, `FixedPortProvider`, `Node.PortProvider`, or any handler/strategy interface.
