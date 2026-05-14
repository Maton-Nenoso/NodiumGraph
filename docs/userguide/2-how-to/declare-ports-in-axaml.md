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

## See also

- [[custom-node-template]] — non-port-declaring `<DataTemplate>` usage.
- [[custom-port-provider]] — when you need a `FixedPortProvider` or `DynamicPortProvider`
  beyond what AXAML covers.
