# Bind ViewportZoom, ViewportOffset, and Selection

## Goal

Two-way bind the canvas viewport (zoom, pan) to a view model so the rest of your UI can read, drive, and persist it — and observe selection changes without touching the canvas directly.

## Prerequisites

- You already host `NodiumGraphCanvas`. If not, see [Host the Canvas](host-canvas.md).
- You have a view model and the usual MVVM plumbing (`INotifyPropertyChanged` / `ReactiveObject` / `ObservableObject`).

## Steps

### 1. What's actually bindable, and what isn't

Two kinds of state are involved here, and they live in different places:

- **Viewport state** is on `NodiumGraphCanvas`. `ViewportZoom` (`double`, default `1.0`) and `ViewportOffset` (`Point`, default `(0, 0)`) are standard Avalonia `StyledProperty` registrations, so they support two-way bindings like any other property. `MinZoom` (`0.1`) and `MaxZoom` (`5.0`) clamp pointer-driven input and are one-way bindable too.
- **Selection state** is on `Graph`, not on the canvas. `Graph.SelectedNodes` is a `ReadOnlyObservableCollection<Node>` — you observe it, you don't bind to it. The canvas mutates it as the user clicks and marquees; the view model watches it and reacts.

### 2. Two-way bind `ViewportZoom` and `ViewportOffset`

Assuming a view model with `Zoom` (`double`) and `PanOffset` (`Point`) properties, the AXAML looks like:

```xml
<ng:NodiumGraphCanvas x:Name="Canvas"
                      Graph="{Binding Graph}"
                      ViewportZoom="{Binding Zoom, Mode=TwoWay}"
                      ViewportOffset="{Binding PanOffset, Mode=TwoWay}"
                      MinZoom="0.25"
                      MaxZoom="4" />
```

Because the canvas uses real styled properties, `PropertyChanged` fires in both directions — the binding stays in sync whether the user zooms with the mouse wheel or your view model assigns `Zoom = 1.5` programmatically.

If you only need to *drive* the viewport from code (fit-to-content button, "zoom to selection"), call the canvas method directly instead of going through a binding:

```csharp
Canvas.ZoomToNodes(Canvas.Graph!.SelectedNodes);
// Or zoom to everything:
Canvas.ZoomToNodes(Canvas.Graph!.Nodes);
```

`ZoomToNodes` computes a fit transform for the supplied nodes (with an optional padding, default `50` px) and writes `ViewportZoom` and `ViewportOffset` in one step — which, thanks to the binding, flows back into your view model.

### 3. Observe selection from a view model

Selection is reported two ways, and you can use either — or both.

**Option A: implement `ISelectionHandler`.** The canvas calls you once per selection change, on the UI thread, with the full post-change set:

```csharp
public sealed class SelectionTracker(MainViewModel vm) : ISelectionHandler
{
    public void OnSelectionChanged(IReadOnlyList<Node> selectedNodes)
    {
        // IMPORTANT: copy if you want to retain this list past the call.
        vm.SelectedNodes = selectedNodes.ToList();
    }
}
```

Wire it up in code-behind:

```csharp
Canvas.SelectionHandler = new SelectionTracker(ViewModel);
```

This is the simplest path if you just want a view-model snapshot.

**Option B: subscribe to `Graph.SelectedNodes` directly.** The collection implements `INotifyCollectionChanged` — but as an explicit interface implementation (a quirk of `ReadOnlyObservableCollection<T>` in Avalonia 12), so you have to cast:

```csharp
var notifying = (INotifyCollectionChanged)graph.SelectedNodes;
notifying.CollectionChanged += (_, _) => RefreshCommandEnablement();
```

Use this when you need delta-level updates (added / removed items) instead of a full replacement, or when a non-canvas component — an outline panel, a commanding layer — needs to react to selection without involving the canvas.

### 4. Bind your own view to the selection count

Once the view model holds the selection list, bindings in other controls become trivial:

```xml
<TextBlock Text="{Binding SelectedNodes.Count, StringFormat='Selected: {0}'}" />
<Button Content="Delete"
        IsEnabled="{Binding SelectedNodes.Count, Converter={StaticResource GreaterThanZeroConverter}}"
        Command="{Binding DeleteSelectedCommand}" />
```

## Full code

```xml
<ng:NodiumGraphCanvas x:Name="Canvas"
                      Graph="{Binding Graph}"
                      ViewportZoom="{Binding Zoom, Mode=TwoWay}"
                      ViewportOffset="{Binding PanOffset, Mode=TwoWay}"
                      MinZoom="0.25"
                      MaxZoom="4" />
```

```csharp
public sealed class MainViewModel : ObservableObject
{
    private double _zoom = 1.0;
    private Point _panOffset;
    private IReadOnlyList<Node> _selectedNodes = Array.Empty<Node>();

    public Graph Graph { get; } = new();

    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, value);
    }

    public Point PanOffset
    {
        get => _panOffset;
        set => SetProperty(ref _panOffset, value);
    }

    public IReadOnlyList<Node> SelectedNodes
    {
        get => _selectedNodes;
        set => SetProperty(ref _selectedNodes, value);
    }
}

// Code-behind, after InitializeComponent():
Canvas.SelectionHandler = new SelectionTracker(ViewModel);

file sealed class SelectionTracker(MainViewModel vm) : ISelectionHandler
{
    public void OnSelectionChanged(IReadOnlyList<Node> selectedNodes)
    {
        vm.SelectedNodes = selectedNodes.ToList();
    }
}
```

## Gotchas

- **`SelectedNodes` is not a styled property on the canvas.** You cannot write `SelectedNodes="{Binding ...}"` in AXAML. Observe the `Graph.SelectedNodes` collection or use `ISelectionHandler` instead.
- **`ReadOnlyObservableCollection<T>.CollectionChanged` is an explicit interface implementation** in Avalonia 12. You must cast the collection to `INotifyCollectionChanged` before subscribing. This is an Avalonia 12 break from earlier versions.
- **Defensive-copy the selection list.** `OnSelectionChanged` receives an `IReadOnlyList<Node>` that the library may reuse internally. If you retain it, call `.ToList()` first.
- **Feedback loops with two-way bindings.** If your view model clamps `Zoom` on set (e.g. to enforce a `MinZoom` lower than the canvas's own), make the setter idempotent — otherwise a small rounding difference can bounce between view model and canvas indefinitely.
- **`ViewportOffset` is in screen pixels, not world units.** The transform is `screen = world * ViewportZoom + ViewportOffset`. If your view model persists offsets, store `ViewportZoom` alongside them or they will not round-trip.
- **`ZoomToNodes` needs measured nodes.** It uses `Node.Width` / `Node.Height`, which are only set after the canvas has measured the corresponding visual. Calling it immediately after adding a node in the same UI frame will produce a zero-sized bounding box; defer to the next layout pass (`Dispatcher.UIThread.Post(...)`) if needed.

## See also

- [Handler interfaces reference](../3-reference/handlers.md)
- [NodiumGraphCanvas control reference](../3-reference/canvas-control.md)
- [Model reference](../3-reference/model.md)
- [Configure pan and zoom gestures](configure-pan-zoom.md)
- [Getting Started tutorial](../1-tutorial/getting-started.md)
