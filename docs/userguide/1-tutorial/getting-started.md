# Getting Started with NodiumGraph

This tutorial walks you through building a minimal Avalonia application with NodiumGraph: a window that hosts an interactive canvas, two nodes ("Source" and "Sink"), and a pre-drawn connection between them. You will also wire up a connection handler so the user can draw new connections by dragging from one port to another. Plan on around 30 minutes.

It assumes you are comfortable with Avalonia basics — AXAML, DataTemplates, code-behind — and modern C#.

> **Screenshot:** The finished app — a light slate canvas with a dotted grid, two indigo-headered rounded nodes labeled "Source" and "Sink", and a bezier curve connecting Source's output port to Sink's input port.

## What you'll build

You will end up with a single Avalonia window that hosts a `NodiumGraphCanvas`. The canvas contains two `MathNode` instances, each with one input and one output port. A connection is pre-wired from `Source.out` to `Sink.in`, and a `GraphConnectionHandler` accepts new connection drags. NodiumGraph's default validator automatically rejects self-connections, same-owner links, same-flow links (output-to-output), and drags between ports whose `DataType` values don't match.

## Prerequisites

- Avalonia 12 and the .NET 10 SDK installed
- Familiarity with AXAML, `DataTemplate`, and code-behind
- A checkout of the NodiumGraph repository — add a `ProjectReference` to `src/NodiumGraph/NodiumGraph.csproj` from your app

The finished code for this tutorial lives in `samples/GettingStarted/`. You can follow along from scratch or open that project directly and read through it.

## 1. Register the NodiumGraph control theme

Before you touch any window, merge NodiumGraph's control theme into your application styles. Without it, `NodePresenter` has no template — nodes render as bare content with no header, no border, and no title text.

Open `App.axaml` and add the `StyleInclude`:

```xml
<!-- from: samples/GettingStarted/App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="GettingStarted.App"
             RequestedThemeVariant="Light">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://NodiumGraph/Themes/Generic.axaml" />
  </Application.Styles>
</Application>
```

`Generic.axaml` ships both `Light` and `Dark` brush palettes through `ResourceDictionary.ThemeDictionaries`, so the canvas picks up the variant set on your application automatically — this tutorial uses `RequestedThemeVariant="Light"`. The include also brings in the `NodePresenter` `ControlTheme` (header bar, body border, collapse toggle). To re-skin the canvas later, override any `NodiumGraph*Brush` key in `Application.Resources`; the [theme-canvas how-to](../2-how-to/theme-canvas.md) lists the complete vocabulary.

## 2. Host NodiumGraphCanvas in a window

Every NodiumGraph app starts with a `NodiumGraphCanvas` somewhere in its layout. It is a `TemplatedControl` in the `NodiumGraph.Controls` namespace, and it renders the nodes, connections, grid, and overlays for you.

Open `MainWindow.axaml` and replace its contents with:

```xml
<!-- from: samples/GettingStarted/MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"
        xmlns:local="clr-namespace:GettingStarted"
        x:Class="GettingStarted.MainWindow"
        Title="NodiumGraph — Getting Started"
        Width="1024" Height="720">

  <Window.DataTemplates>
    <DataTemplate DataType="local:MathNode">
      <ng:NodePresenter HeaderBackground="#6366F1"
                        HeaderForeground="White"
                        HeaderPadding="12,8"
                        CornerRadius="8">
        <TextBlock Text="{Binding Description}"
                   Margin="12,8"
                   Foreground="#475569"
                   FontSize="12" />
      </ng:NodePresenter>
    </DataTemplate>
  </Window.DataTemplates>

  <ng:NodiumGraphCanvas x:Name="Canvas"
                        ShowGrid="True" />
</Window>
```

A few things to notice:

- `xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"` imports the canvas and related controls.
- `x:Name="Canvas"` gives the generated partial class a field named `Canvas` so the code-behind can assign the graph.
- `ShowGrid="True"` enables the default dot grid.
- Pan (middle-mouse drag or `Space`+left-drag), zoom (scroll wheel), and selection (click, Ctrl-click, marquee) all work out of the box — no wiring required.

The `Window.DataTemplates` block is how the canvas learns to render your node type. Step 5 explains what `NodePresenter` gives you.

## 3. Create a node class

`Node` is a concrete, unsealed base class in `NodiumGraph.Model`. You subclass it to attach domain data — not because the model is incomplete, but because the DataTemplate system resolves templates by `DataType="local:MathNode"`. Without a subclass there is nothing to key the template off of.

Create `MathNode.cs` next to `MainWindow.axaml.cs`:

```csharp
// from: samples/GettingStarted/MathNode.cs
using NodiumGraph.Model;

namespace GettingStarted;

public class MathNode : Node
{
    public string Description { get; set; } = string.Empty;
}
```

`Title`, `X`, `Y`, and `PortProvider` are already on `Node` — do not redeclare them. `Width` and `Height` are set internally by the canvas during measure, so leave those alone as well.

## 4. Give the node ports

Connections attach to ports, not to nodes. Each port has a flow direction (`PortFlow.Input` or `PortFlow.Output`) and an optional `DataType` token that the default validator compares by equality.

Add this helper to `MainWindow.axaml.cs`:

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
private static MathNode CreateMathNode(string title, string description, double x, double y)
{
    var node = new MathNode
    {
        Title = title,
        Description = description,
        X = x,
        Y = y,
    };

    var provider = new FixedPortProvider(layoutAware: true);
    provider.AddPort(new Port(node, "in", PortFlow.Input, new Point(0, 40))
    {
        Label = "in",
        DataType = "number",
    });
    provider.AddPort(new Port(node, "out", PortFlow.Output, new Point(180, 40))
    {
        Label = "out",
        DataType = "number",
    });

    node.PortProvider = provider;
    return node;
}
```

Notes:

- `new FixedPortProvider(layoutAware: true)` snaps each port's position to the node's measured boundary, so rough coordinates like `(0, 40)` (left edge, roughly middle) and `(180, 40)` (right edge) work without knowing the node's final width.
- Each port has a `Name` unique within the provider (`"in"`, `"out"`). The `Label` is the text rendered next to the port.
- `DataType = "number"` is an opaque token. Matching values connect; mismatched values are rejected by the default validator.

> **Gotcha:** `Port.Position` is **node-local**, not world-space. Use `Port.AbsolutePosition` when you need world coordinates — it is computed on demand from the owning node.

## 5. Write the node DataTemplate

When the canvas materializes a node, it looks up a `DataTemplate` keyed by the node's concrete type and instantiates it inside a node host. Rooting your template with `<ng:NodePresenter>` gives you the standard node chrome: a header bar, body area, corner radius, and the hover/selection visuals. You only need to supply the body content.

The template block from the AXAML file is:

```xml
<!-- from: samples/GettingStarted/MainWindow.axaml -->
<DataTemplate DataType="local:MathNode">
  <ng:NodePresenter HeaderBackground="#6366F1"
                    HeaderForeground="White"
                    HeaderPadding="12,8"
                    CornerRadius="8">
    <TextBlock Text="{Binding Description}"
               Margin="12,8"
               Foreground="#475569"
               FontSize="12" />
  </ng:NodePresenter>
</DataTemplate>
```

`HeaderBackground`, `HeaderForeground`, `HeaderPadding`, and `CornerRadius` are styled properties on `NodePresenter`, so you get theming without writing a full control template. The body here is a single `TextBlock` bound to `MathNode.Description`, but you can drop any Avalonia content in there — `StackPanel`, inputs, images, whatever fits your domain.

Ports are rendered automatically by the canvas from `node.PortProvider.Ports`. Do **not** place port visuals inside the DataTemplate — the template is only responsible for the node body.

See [how to define a custom node DataTemplate](../2-how-to/custom-node-template.md) for more on `NodePresenter` and template patterns.

## 6. Build the graph

A `Graph` is just a container for nodes and connections. Add the builder:

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
private static Graph BuildGraph()
{
    var graph = new Graph();

    var source = CreateMathNode("Source", "Produces a number", x: 120, y: 200);
    var sink = CreateMathNode("Sink", "Consumes a number", x: 480, y: 200);

    graph.AddNode(source);
    graph.AddNode(sink);

    var sourceOut = source.PortProvider!.Ports[1];
    var sinkIn = sink.PortProvider!.Ports[0];
    graph.AddConnection(new Connection(sourceOut, sinkIn));

    return graph;
}
```

`CreateMathNode` returns a fully configured node. `graph.AddNode(node)` registers it with the graph, which raises the collection-change events the canvas subscribes to — so the node appears immediately. The two ports are indexed positionally: `Ports[0]` is `in` (added first) and `Ports[1]` is `out`. In real code, prefer a named lookup such as `source.PortProvider!.Ports.First(p => p.Name == "out")` — it is more resilient to future reordering.

Finally, `graph.AddConnection(new Connection(sourceOut, sinkIn))` pre-wires one connection before the canvas ever renders.

> **Gotcha:** `Graph.Nodes` and `Graph.Connections` are exposed as `ReadOnlyObservableCollection<T>`. Always go through `AddNode` / `RemoveNode` / `AddConnection` / `RemoveConnection` — never mutate those collections directly.

## 7. Wire the connection handler

NodiumGraph follows a "report, don't decide" rule. When the user completes a connection drag, the canvas calls `IConnectionHandler.OnConnectionRequested(source, target)` and expects **your code** to decide whether to accept the connection and to mutate the graph itself. The canvas will not add the connection for you.

Add this handler at the bottom of `MainWindow.axaml.cs`:

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
file sealed class GraphConnectionHandler(Graph graph) : IConnectionHandler
{
    public Result<Connection> OnConnectionRequested(Port source, Port target)
    {
        var connection = new Connection(source, target);
        graph.AddConnection(connection);
        return connection;
    }

    public void OnConnectionDeleteRequested(Connection connection)
    {
        graph.RemoveConnection(connection);
    }
}
```

`OnConnectionRequested` returns `Result<Connection>`. The `return connection;` uses an implicit operator that converts a `Connection` into a successful `Result<Connection>`. To reject the request, return an `Error` instead — there is a matching implicit operator. `OnConnectionDeleteRequested` is the symmetric hook: when your app triggers a delete, the handler is responsible for calling `RemoveConnection`.

Finally, wire everything up in the constructor:

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
Canvas.Graph = graph;
Canvas.ConnectionHandler = new GraphConnectionHandler(graph);
```

The full `MainWindow.axaml.cs` builds the graph, assigns it, and installs the handler in one shot.

See [handler interfaces reference](../3-reference/handlers.md) for the complete handler contracts and [Result pattern](../3-reference/result-pattern.md) for `Result<T>` and `Error`.

## 8. Run it and experiment

From the repo root, launch the sample:

```bash
dotnet run --project samples/GettingStarted/GettingStarted.csproj
```

> **Screenshot:** The running app — two rounded indigo-headered nodes labeled "Source" and "Sink" connected by a gray bezier curve, on a dotted grid background.

Try the following to build intuition for how the canvas behaves:

1. Drag the nodes around. Positions update live and the connection re-routes.
2. Drag from `Source.out` into empty space. You will see a live preview of the in-progress connection; releasing on empty canvas cancels it.
3. Drag from `Sink.out` to an empty area. The drag shows an in-progress preview but is rejected on release because there is no target port.
4. Drag from `Source.out` back onto one of `Source`'s own ports. The preview shows a rejected state because the default validator blocks same-owner drags.
5. Drag from `Source.out` to `Sink.out`. Also rejected — same flow direction.

`DefaultConnectionValidator.Instance` runs whenever you have not supplied a custom validator. It rejects:

- same-port self-connections
- same-owner connections (two ports on the same node)
- same-flow connections (output-to-output or input-to-input)
- mismatched `DataType` values (equality check; `null` matches only `null`)

To see the `DataType` rule in action, change `CreateMathNode` so the `sink` call builds its `in` port with `DataType = "string"` instead of `"number"`. Rebuild and drag from `Source.out` to `Sink.in` — the live preview now shows the rejected state because the tokens do not match. Revert the change when you are done.

> **Note:** The validator runs during the drag, not after the user releases. That is why invalid drags show a visually distinct rejected preview rather than being silently dropped at commit time.

## Where next

- [How to define a custom node DataTemplate](../2-how-to/custom-node-template.md) — deeper dive on `NodePresenter` and template composition
- [How to handle node moves for undo/redo](../2-how-to/handle-node-moves-undo.md) — wire `INodeInteractionHandler` and capture old/new positions
- [How to write a custom `IConnectionValidator`](../2-how-to/custom-validator.md) — rules beyond `DataType` equality
- [How to style ports](../2-how-to/style-ports.md) — port shapes, labels, and hover states
- [Handler interfaces reference](../3-reference/handlers.md) — full contracts for every handler
- [Report, don't decide](../4-explanation/report-dont-decide.md) — the philosophy behind the handler pattern

The finished code for this tutorial lives in `samples/GettingStarted/` — open it any time you want to compare against your own project.
