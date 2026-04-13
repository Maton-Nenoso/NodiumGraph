# Host the Canvas in a Window or User Control

## Goal

Add a `NodiumGraphCanvas` to an existing Avalonia layout, attach a `Graph`, and have it render. This is the first thing every NodiumGraph app does.

## Prerequisites

- An Avalonia 12 / .NET 10 project
- A `ProjectReference` to `src/NodiumGraph/NodiumGraph.csproj`
- A subclass of `Node` for your domain (see [Subclass the model](subclass-model.md))

## Steps

### 1. Add the XML namespace

The canvas lives in `NodiumGraph.Controls`, inside the `NodiumGraph` assembly. Add this to the root element of any AXAML file that uses it:

```xml
xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"
```

The `ng:` prefix is a convention, not a requirement — pick whatever you like.

### 2. Declare a template for your node type

The canvas renders nodes via Avalonia's regular `DataTemplate` system, keyed on the CLR type. Put the template in `Window.DataTemplates` (or `UserControl.DataTemplates`) so every `NodiumGraphCanvas` in that scope picks it up:

```xml
<Window.DataTemplates>
  <DataTemplate DataType="local:MathNode">
    <ng:NodePresenter HeaderBackground="#6366F1" HeaderForeground="White" CornerRadius="8">
      <TextBlock Text="{Binding Description}" Margin="12,8" />
    </ng:NodePresenter>
  </DataTemplate>
</Window.DataTemplates>
```

`NodePresenter` is a light wrapper that gives you a styled header bar and a content slot. You can substitute any visual tree you want — it just has to render the node. See [Define a custom node DataTemplate](custom-node-template.md) for the full set of options.

### 3. Drop the canvas into your layout

```xml
<ng:NodiumGraphCanvas x:Name="Canvas"
                      Background="#F1F5F9"
                      ShowGrid="True" />
```

Naming the canvas gives you a generated field in the code-behind, which you use to assign the `Graph` and optionally any handlers.

### 4. Wire the graph from code-behind

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var graph = new Graph();
        // ...populate nodes and connections...
        Canvas.Graph = graph;
    }
}
```

With `Canvas.Graph` set, pan (middle-mouse or `Space`+left-drag), zoom (scroll wheel / pinch), and selection (click, Ctrl+click, marquee) work immediately — no additional wiring needed. To let users *create* connections rather than just view them, assign a `ConnectionHandler`; see [Getting Started](../tutorial/getting-started.md#6-wire-the-connection-handler).

## Full code

The working version of this recipe is `samples/GettingStarted/`, which builds and runs as a standalone Avalonia app.

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
        <TextBlock Text="{Binding Description}" Margin="12,8" />
      </ng:NodePresenter>
    </DataTemplate>
  </Window.DataTemplates>

  <ng:NodiumGraphCanvas x:Name="Canvas"
                        Background="#F1F5F9"
                        ShowGrid="True" />
</Window>
```

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NodiumGraph.Model;

namespace GettingStarted;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var graph = new Graph();
        graph.AddNode(new MathNode { X = 120, Y = 200, Description = "Source" });
        graph.AddNode(new MathNode { X = 480, Y = 200, Description = "Sink" });

        Canvas.Graph = graph;
    }
}
```

### Hosting inside a `UserControl`

The setup is identical — swap `Window` for `UserControl`, put the template in `UserControl.DataTemplates`, and assign `Canvas.Graph` in the code-behind the same way. Nothing else changes.

## Gotchas

- **`Canvas.Graph` defaults to `null`.** Until you assign a `Graph`, the canvas renders its background and grid but nothing else, and every interaction becomes a no-op. This is intentional — it lets you bind the graph asynchronously without special-casing startup.
- **Templates resolve by CLR type, not by `Id`.** If your `DataTemplate` targets the base `Node` type, Avalonia uses it for *every* node subclass. Target the concrete subclass (`DataType="local:MathNode"`) if you want per-type visuals.
- **The canvas never mutates your `Graph`.** Nothing the user does — dragging, marqueeing, connecting, deleting — touches the model unless a handler you wrote explicitly calls `graph.AddNode` / `graph.AddConnection` / etc. See [Report, don't decide](../explanation/report-dont-decide.md).
- **UI-thread only.** All canvas properties and handler callbacks are expected on the Avalonia UI thread. If you load a graph from a background task, marshal the `Canvas.Graph = ...` assignment through `Dispatcher.UIThread.Post`.
- **The `ng:` prefix can clash with other libraries.** Pick a different alias (for example `nodium:`) if you already use `ng:` for Angular-style tooling or another Avalonia package.

## See also

- [Getting Started tutorial](../tutorial/getting-started.md)
- [NodiumGraphCanvas control reference](../reference/canvas-control.md)
- [Define a custom node DataTemplate](custom-node-template.md)
- [Bind ViewportZoom, ViewportOffset, SelectedNodes](bind-viewport.md)
- [Handler interfaces reference](../reference/handlers.md)
