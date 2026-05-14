---
title: Declarative AXAML port definitions
tags: [plan, spec]
status: active
created: 2026-05-14
updated: 2026-05-14
---

# Declarative AXAML port definitions

Today, ports are declared in C# only: a consumer instantiates `Port(node, name, flow, anchor)` and wraps them in a `FixedPortProvider` assigned to `Node.PortProvider`. The DataTemplate side of NodiumGraph already drives node *visuals* via `<ng:NodePresenter>`, but the port topology must be wired separately in code-behind. This design lets a consumer declare a fixed port set inline in the DataTemplate, next to where the node's chrome is themed.

## Goals

- Declare a node's fixed port set in AXAML, on the same `NodePresenter` that themes the node body.
- Materialization is one-time and idempotent: re-applying the same template to the same `Node` does not stack ports.
- **Code wins**: if the consumer assigns `Node.PortProvider` in code, the AXAML declaration is silently ignored.
- No new model surface. `Port`, `PortAnchor`, `FixedPortProvider`, and `Node.PortProvider` are unchanged.

## Non-goals

- **Bindings on port fields.** `PortDefinition` is literal-only. `Edge`/`Fraction` carry anchor identity — binding-driven changes would force port recreation and detach live connections. Static covers the realistic use case.
- **Runtime port mutation through the AXAML pipeline.** Add/remove/relabel after materialization stays imperative against the model.
- **DynamicPortProvider in AXAML.** Its purpose is "create a port at the hit point on drag," which is imperative. Setting a `DynamicPortProvider` from code remains the answer.
- **Port set swap on template change.** Once a `PortProvider` exists on a node, AXAML re-evaluation is a no-op. Swapping port topology at runtime is a code-side concern.
- **Default template port declarations.** AXAML declaration only takes effect when the consumer supplies a custom DataTemplate; the built-in `DefaultTemplates.NodeTemplate` does not expose ports declaratively. (Consumers who want ports without a custom template still set `PortProvider` in code.)

## Design

### `PortDefinition`

Lightweight POCO. Lives in `NodiumGraph.Controls` because it is tightly coupled to `NodePresenter`; it carries no model state and is only a construction recipe.

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

- Two attributes for the anchor (`Edge`, `Fraction`) — matches `PortAnchor(Edge, Fraction)` one-for-one and gives Avalonia XAML completion for the `PortEdge` enum values. No type converter; a converter is a clean, non-breaking addition later if terseness demands it.
- `Name` is required at materialization time (empty string is allowed but discouraged; `FixedPortProvider` does not enforce uniqueness today and this design does not change that).
- All other fields map directly to `Port` properties or constructor arguments.

### `NodePresenter.Ports`

A read-only CLR collection property on `NodePresenter`. Not a `StyledProperty` — the list is read once at attach time and never observed for changes.

```csharp
public IList<PortDefinition> Ports { get; } = new List<PortDefinition>();
```

XAML usage:

```xml
<DataTemplate DataType="local:InputSourceNode">
  <ng:NodePresenter HeaderBackground="#10B981">
    <ng:NodePresenter.Ports>
      <ng:PortDefinition Name="in"  Flow="Input"  Edge="Left"  Fraction="0.5" />
      <ng:PortDefinition Name="out" Flow="Output" Edge="Right" Fraction="0.5" Label="result" />
    </ng:NodePresenter.Ports>
    <TextBlock Text="..." />
  </ng:NodePresenter>
</DataTemplate>
```

### Materialization

Hook: `NodePresenter.OnAttachedToVisualTree`.

```text
on attached:
    if DataContext is not Node node: return
    if node.PortProvider is not null: return            // code wins
    if Ports.Count == 0: return                          // empty list ≡ no AXAML declaration
    var ports = Ports.Select(d => new Port(node, d.Name, d.Flow,
                                           new PortAnchor(d.Edge, d.Fraction))
                              {
                                  Label = d.Label,
                                  MaxConnections = d.MaxConnections,
                                  DataType = d.DataType,
                              }).ToList();
    node.PortProvider = new FixedPortProvider(ports);
```

Properties of this rule:

- **Idempotent per node.** Once `PortProvider` is set, any subsequent `NodePresenter` attached to the same `Node` (re-template, second presenter in a minimap, etc.) sees a non-null provider and no-ops.
- **Code wins.** A consumer who has assigned `PortProvider` in code retains it. The AXAML declaration becomes a silent fallback, never an override.
- **No detach action.** Unsubscribing on detach would create a lifecycle leak between view and model. The provider lives on the model and stays there for as long as the node lives.
- **Empty list ≡ omission.** Consumers who genuinely want no ports just leave `<ng:NodePresenter.Ports>` out. The empty-list case is treated the same: no provider is created, leaving the node with no PortProvider (the existing default).

### Error handling

- `PortAnchor(Edge, Fraction)` already validates both fields at construction; out-of-range `Fraction` or undefined `PortEdge` throws `ArgumentOutOfRangeException` at materialization. Surfaced as an unhandled exception during the first attach — a hard fail by design, since AXAML errors should not be silently swallowed.
- Null `Name` is rejected by `Port(Node, string, ...)` and propagates the same way.
- Duplicate names are accepted (matches current `FixedPortProvider` behavior).

## Open question resolutions

| Q | Resolution | Reason |
|---|------------|--------|
| Anchor syntax | Two attributes (`Edge`, `Fraction`) | Discoverability; mirrors `PortAnchor` record shape; converter is a non-breaking add later. |
| Re-template behavior | Ignore (provider exists → AXAML silent) | Matches "code wins"; avoids hidden state markers; protects live connections. |
| Bindings on `PortDefinition` | POCO, literal-only | `Edge`/`Fraction` carry identity and can't be safely mutated; runtime label changes happen in code against `port.Label`; promoting to `AvaloniaObject` later is non-breaking. |
| `DynamicPortProvider` in AXAML | Punt | Imperative by nature; trivial to add later if requested. |

## Testing

xUnit v3 + Avalonia headless. New file `tests/NodiumGraph.Tests/DeclarativePortsTests.cs`:

- **Materialize once on first attach.** Attach `NodePresenter` with two `PortDefinition`s to a node whose `PortProvider` is null → provider becomes a `FixedPortProvider` with two ports whose `Anchor.Edge`/`Anchor.Fraction`/`Name`/`Flow` match.
- **Optional fields propagate.** `Label`, `MaxConnections`, `DataType` flow through to the materialized `Port`.
- **Code wins.** Pre-assign `node.PortProvider = …` before attach → attach is a no-op; the pre-existing provider is the one observed.
- **Empty list is a no-op.** Attach with `Ports.Count == 0` → `node.PortProvider` stays null.
- **Re-attach is a no-op.** Attach, detach, re-attach the same `NodePresenter` to the same node → port count remains 2; the provider reference is the same instance.
- **Second presenter on same node.** Construct two `NodePresenter`s with different `Ports` lists, both bound to the same node → only the first list materializes; the second is silently dropped.
- **Invalid `Fraction` throws.** `Fraction = 1.5` → attach throws `ArgumentOutOfRangeException` (propagated from `PortAnchor`).

Integration test against the live canvas is not required for this surface: the materialization happens on the presenter, and existing canvas tests already cover provider-driven port rendering and hit-testing.

## Documentation impact

- New how-to: `docs/userguide/2-how-to/declare-ports-in-axaml.md`. Covers the `<ng:NodePresenter.Ports>` form, the "code wins" rule, and one end-to-end example.
- Update `docs/userguide/2-how-to/custom-node-template.md` to cross-reference the new how-to in a "Adding ports" section.
- Update `docs/userguide/2-how-to/custom-port-provider.md` to note that for fixed sets, AXAML declaration is now the recommended path; the code-side `FixedPortProvider` example remains as the override / dynamic-construction path.
- No reference-doc additions for `NodePresenter` until a `node-presenter.md` reference page exists (out of scope for this spec; tracked separately).

## Out of scope / future

- Markup-extension form for terse anchors (`Anchor="Left:0.5"`) — additive.
- `PortDefinition : AvaloniaObject` with `StyledProperty` for `Label`/`DataType` to allow bindings — additive.
- `<ng:NodePresenter.PortProvider>` content slot to declare any `IPortProvider` (including `DynamicPortProvider`) — additive.
- A default template variant that surfaces `Ports` so consumers can declare ports without writing their own `<ng:NodePresenter>` — additive; needs UX thought on where ports render when there is no body.

## Public surface delta

Added (single namespace, `NodiumGraph.Controls`):

- `PortDefinition` (POCO).
- `NodePresenter.Ports` (`IList<PortDefinition>`).

No other public surface changes. No model changes. No new dependencies.
