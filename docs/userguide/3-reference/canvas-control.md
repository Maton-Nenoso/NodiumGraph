# NodiumGraphCanvas Control Reference

`NodiumGraphCanvas` is the primary control the library ships. It is a `TemplatedControl` that hosts a `Graph`, renders its nodes via an Avalonia `DataTemplate`, and custom-draws the grid, connections, selection overlay, and minimap. All built-in interactions (pan, zoom, selection, node drag, connection draw) are wired into this control — consumers participate by binding a `Graph` and optionally assigning handler / strategy implementations on the properties documented below. See the [model reference](model.md) for the `Graph` / `Node` / `Port` / `Connection` types this control operates on, and the [handlers](handlers.md) and [strategies](strategies.md) references for the interfaces you can plug in.

## Namespace and assembly

- Namespace: `NodiumGraph.Controls`
- Assembly: `NodiumGraph`
- AXAML declaration: `xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"`
- Base class: `Avalonia.Controls.Primitives.TemplatedControl`
- Also implements: `Avalonia.Rendering.ICustomHitTest`, `IDisposable`

## Styled properties

Every consumer-facing property on the canvas is registered as a standard Avalonia `StyledProperty`, so they are all bindable and stylable. Defaults below are verified against `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`.

### Graph and templates

| Property | Type | Default | Description |
|---|---|---|---|
| `Graph` | `Graph?` | `null` | The graph document rendered by this canvas. All other interactions are no-ops until this is set. |
| `NodeTemplate` | `IDataTemplate?` | `null` | Optional global template used for every node. When `null`, the canvas falls back to whichever `DataTemplate` the parent control tree provides for the node type, or to a built-in default. `IDataTemplate` lives in `Avalonia.Controls.Templates`. |
| `PortTemplate` | `IDataTemplate?` | `null` | Optional template for port visuals. When `null`, each node's adornment layer custom-draws that node's ports directly using `PortStyle`. |

### Viewport

| Property | Type | Default | Description |
|---|---|---|---|
| `ViewportZoom` | `double` | `1.0` | Current zoom factor. Two-way bindable. |
| `ViewportOffset` | `Point` | `(0, 0)` | Current pan offset in screen pixels. The transform is `screen = world * ViewportZoom + ViewportOffset`. |
| `MinZoom` | `double` | `0.1` | Lower clamp applied during wheel / pinch input. |
| `MaxZoom` | `double` | `5.0` | Upper clamp applied during wheel / pinch input. |

### Grid

| Property | Type | Default | Description |
|---|---|---|---|
| `ShowGrid` | `bool` | `true` | Toggles the grid layer. |
| `GridSize` | `double` | `20.0` | Spacing between minor grid lines, in world units. |
| `GridStyle` | `GridStyle` | `GridStyle.Dots` | One of `Dots`, `Lines`, `None`. |
| `MajorGridInterval` | `int` | `5` | Every Nth minor cell is drawn with the "major" brush. |
| `ShowOriginAxes` | `bool` | `true` | Draws the X and Y axes through world origin `(0, 0)`. |
| `SnapToGrid` | `bool` | `false` | When `true`, dragged nodes snap to `GridSize` increments on drag completion. |
| `ShowSnapGhost` | `bool` | `false` | When combined with `SnapToGrid`, renders a ghost outline at the snapped position during drag. |

### Connections

| Property | Type | Default | Description |
|---|---|---|---|
| `ConnectionRouter` | `IConnectionRouter` | `new BezierRouter()` | Strategy that computes path points for each connection. See [strategies reference](strategies.md). |
| `DefaultConnectionStyle` | `IConnectionStyle` | `new ConnectionStyle()` | Stroke, thickness, and dash pattern used by the connection renderer. `ConnectionStyle` defaults to gray, 2px, solid. |
| `ConnectionValidator` | `IConnectionValidator?` | `DefaultConnectionValidator.Instance` | Accept/reject predicate used for live feedback during a connection drag. Set to `null` to disable built-in checks. |

### Interaction handlers

All handler properties are nullable — the canvas functions fine when they are unset, but the corresponding interaction is reported nowhere and the graph is never mutated by the library itself.

| Property | Type | Default | Description |
|---|---|---|---|
| `NodeHandler` | `INodeInteractionHandler?` | `null` | Receives node move / delete / double-click events. |
| `ConnectionHandler` | `IConnectionHandler?` | `null` | Receives connection request / delete events. Without this, completed drags are discarded. |
| `SelectionHandler` | `ISelectionHandler?` | `null` | Receives selection-changed notifications. |
| `CanvasHandler` | `ICanvasInteractionHandler?` | `null` | Receives canvas double-click and external drag-drop events. |

See the [handler interfaces reference](handlers.md) for the contract of each member.

### Minimap

| Property | Type | Default | Description |
|---|---|---|---|
| `ShowMinimap` | `bool` | `false` | Toggles the minimap overlay in a corner of the canvas. |
| `MinimapPosition` | `MinimapPosition` | `MinimapPosition.BottomRight` | One of `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`. |

## AXAML usage

The Getting Started sample shows a minimal canvas declaration:

```xml
<!-- from: samples/GettingStarted/MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"
        xmlns:local="clr-namespace:GettingStarted"
        x:Class="GettingStarted.MainWindow">

  <Window.DataTemplates>
    <DataTemplate DataType="local:MathNode">
      <ng:NodePresenter HeaderBackground="#6366F1"
                        HeaderForeground="White"
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

The graph and handler are wired from code-behind:

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
public MainWindow()
{
    AvaloniaXamlLoader.Load(this);

    var graph = BuildGraph();
    Canvas.Graph = graph;
    Canvas.ConnectionHandler = new GraphConnectionHandler(graph);
}
```

## Built-in interactions

These work out of the box without wiring a single handler:

- **Pan:** middle-mouse drag, or Space+left-drag
- **Zoom:** scroll wheel (toward cursor), pinch gesture, or programmatic via `ViewportZoom`
- **Selection:** left-click (clear + select), Ctrl+click (toggle), marquee drag, Ctrl+marquee (additive)
- **Node drag:** left-drag; dragging a selected node drags the whole selection; reports after drag completes, never during
- **Connection draw:** left-drag from a port; hover shows live accept / reject feedback via `ConnectionValidator`; release on a valid target or on empty space to cancel
- **Connection cut:** right-drag across routed connections (slice gesture)

What the canvas *reports* for each of these — and where your application code gets to make decisions — is described in the [handlers reference](handlers.md).

## Events

`NodiumGraphCanvas` does not expose consumer-facing routed events. Interactions are reported through the handler interfaces listed above (`INodeInteractionHandler`, `IConnectionHandler`, `ISelectionHandler`, `ICanvasInteractionHandler`). See the [handler interfaces reference](handlers.md).

Standard Avalonia property-change notifications (`PropertyChanged`) still fire for every styled property, so `ViewportZoom` and `ViewportOffset` can be two-way bound without any extra plumbing.

## See also

- [Handler interfaces reference](handlers.md)
- [Strategy interfaces reference](strategies.md)
- [Model reference](model.md)
- [Rendering pipeline reference](rendering-pipeline.md)
- [Getting Started tutorial](../1-tutorial/getting-started.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
