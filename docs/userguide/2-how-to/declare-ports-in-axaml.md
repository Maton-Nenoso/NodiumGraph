---
title: Declare ports in AXAML
tags: [how-to]
status: active
created: 2026-05-14
updated: 2026-05-19
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

`<ng:NodeTemplate>` is an `IDataTemplate` like `<DataTemplate>`, but it also accepts
declarative port topology under `<NodeTemplate.Ports>`. At XAML load, the declared ports
are registered into a process-wide `NodePortRegistry` keyed by `DataType`.

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

`Node.Ports` (and the existing `Node.PortProvider`) lazy-materialize a `FixedPortProvider`
from the registry on first read. The materialization happens once per node instance.

## Rules to remember

- **Exact-type matching.** `<ng:NodeTemplate DataType="local:Base">` does not apply to derived
  types. Each concrete node type that needs declared ports gets its own `<ng:NodeTemplate>`.
- **Code wins.** Setting `node.PortProvider` (including `= null`) permanently suppresses the
  registry for that node instance. Use this to override AXAML defaults for a specific node.
- **Mixed templates.** Use plain `<DataTemplate>` when you want polymorphic visual matching
  (Avalonia's default `IsInstanceOfType`) without declared ports — and wire ports in code, or
  declare a `<ng:NodeTemplate>` per concrete derived type.
- **Visual-only `<ng:NodeTemplate>`.** Omit `<NodeTemplate.Ports>` and the template behaves
  like a plain `<DataTemplate>` — no registration, no port materialization.

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

`Mid1` and `Mid2` distribute over the whole edge using `(i+1)/3` → `0.333` and
`0.667`. They sit between the two pinned ports because the formula naturally places
them in that range when there are only two auto ports, but the library does not look
at the pinned positions to compute auto fractions.

### Runtime add/remove

Adding or removing an auto port through `FixedPortProvider.AddPort` / `RemovePort`
re-runs the distribution on that edge. The provider fires `PortAdded` / `PortRemoved`
after the layout pass completes, so subscribers always observe a fully-laid-out
collection.

## See also

- [[custom-node-template]] — non-port-declaring `<DataTemplate>` usage.
- [[custom-port-provider]] — when you need a `FixedPortProvider` or `DynamicPortProvider`
  beyond what AXAML covers.
