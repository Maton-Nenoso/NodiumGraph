# Configure Pan and Zoom Gestures

## Goal

Clamp how far users can zoom, lock the zoom level to a single value, drive the viewport programmatically, and understand what you can and cannot customise about the built-in pan gestures.

## Prerequisites

- You already host `NodiumGraphCanvas` and have it rendering a graph. See [Host the Canvas](host-canvas.md).
- You've seen the viewport-binding recipe — pan and zoom are usually shaped through two-way bindings on `ViewportZoom` / `ViewportOffset`. See [Bind viewport state](bind-viewport.md).

## Steps

### 1. What the canvas gives you out of the box

The built-in interactions run from the moment you assign a `Graph`:

- **Pan:** middle-mouse drag, or `Space`+left-drag
- **Zoom:** scroll wheel (zoom toward pointer), pinch gesture, or programmatic assignment to `ViewportZoom`
- **Auto-pan:** during a connection drag near the canvas edge, the viewport scrolls toward the pointer

These gestures are **hardcoded** in the canvas. There is no public API to disable them, rebind the keys, or swap middle-mouse for a different button. If you need that level of customization today, it's a library change, not a consumer how-to. What you *can* control is the target of those gestures (via `ViewportZoom` and `ViewportOffset`) and the clamp range (via `MinZoom` and `MaxZoom`).

### 2. Clamp the zoom range

`MinZoom` and `MaxZoom` are styled properties on the canvas. They bound wheel and pinch input; programmatic assignments to `ViewportZoom` are also clamped on the next user gesture, so it's cleanest to assign within-range values from your own code too.

```xml
<ng:NodiumGraphCanvas MinZoom="0.25" MaxZoom="4.0" />
```

Defaults are `0.1` and `5.0`. Tighten them for:

- **Overview-heavy diagrams** — raise `MinZoom` so users can't zoom out past a useful overview level
- **Detail-heavy diagrams** — lower `MaxZoom` so users can't zoom in past a point where a single pixel becomes a whole node
- **Presentations and tutorials** — clamp both to a narrow range so the viewport can't wander

### 3. Lock the zoom level

To prevent users from zooming at all, set `MinZoom` and `MaxZoom` to the same value:

```csharp
Canvas.MinZoom = 1.0;
Canvas.MaxZoom = 1.0;
Canvas.ViewportZoom = 1.0;
```

Wheel and pinch still *try* to zoom — the canvas accepts the input event — but the clamp kicks the value back to `1.0` immediately, so the visible zoom never changes. This is the supported way to turn zoom off. Users can still pan.

### 4. Drive the viewport programmatically

Two methods on the canvas frame the viewport for you:

- **`Canvas.ZoomToFit(double padding = 50.0)`** — zooms out until every node in the graph fits inside the visible area, with `padding` pixels of slack on each side. Short for `ZoomToNodes(Graph.Nodes, padding)`.
- **`Canvas.ZoomToNodes(IEnumerable<Node> nodes, double padding = 50.0)`** — zooms and pans so the supplied subset fits. Useful for "zoom to selection" buttons and "focus on this area" workflows.

Both write `ViewportZoom` and `ViewportOffset` atomically, so if you two-way-bound them to your view model the values flow back out automatically. Both clamp the resulting zoom to `MinZoom` / `MaxZoom`, so a fit-to-content on an empty viewport degrades gracefully.

```csharp
// Fit-all button:
FitAllButton.Click += (_, _) => Canvas.ZoomToFit();

// Zoom-to-selection:
ZoomSelectionButton.Click += (_, _) =>
{
    if (Canvas.Graph is { SelectedNodes: { Count: > 0 } selected })
        Canvas.ZoomToNodes(selected);
};

// Reset viewport:
ResetButton.Click += (_, _) =>
{
    Canvas.ViewportZoom = 1.0;
    Canvas.ViewportOffset = new Point(0, 0);
};
```

### 5. What you can't (currently) do

Writing these down so you don't waste time looking:

- **Disable pan entirely.** There is no `EnablePan` / `IsPanEnabled` flag. The canvas will always pan on middle-mouse and `Space`+left-drag when they occur.
- **Rebind the pan key.** `Space` is hardcoded. If your app already uses `Space` for something else, the conflict is on you to resolve (hardest case: intercept the `KeyDown` event on a parent control and mark it handled before the canvas sees it).
- **Disable wheel zoom.** There is no `IsZoomEnabled`. The clamp trick in step 3 is the closest the library comes to "no zoom".
- **Customise the auto-pan margin or speed.** The edge-trigger margin (40 world units) and scroll speed (10 units / frame) are private constants today.

If you need any of the above, open an issue or send a PR — they're small surface changes, just not in the library yet.

### 6. Two-way bind pan and zoom to your view model

Covered in detail in [Bind ViewportZoom, ViewportOffset, and Selection](bind-viewport.md). Summarised here: both properties are styled, standard two-way bindings work, and `PropertyChanged` fires when the user pans or zooms with gestures.

```xml
<ng:NodiumGraphCanvas ViewportZoom="{Binding Zoom, Mode=TwoWay}"
                      ViewportOffset="{Binding PanOffset, Mode=TwoWay}"
                      MinZoom="0.25"
                      MaxZoom="4" />
```

This is the idiomatic path for any app that needs a "zoom: 150%" label, a "reset view" button in the toolbar, or persistence of the user's last viewport across sessions.

## Full code

```xml
<Grid ColumnDefinitions="*, Auto">
  <ng:NodiumGraphCanvas x:Name="Canvas"
                        Grid.Column="0"
                        Background="#F1F5F9"
                        ShowGrid="True"
                        MinZoom="{Binding MinZoom}"
                        MaxZoom="{Binding MaxZoom}"
                        ViewportZoom="{Binding Zoom, Mode=TwoWay}"
                        ViewportOffset="{Binding PanOffset, Mode=TwoWay}" />

  <StackPanel Grid.Column="1" Spacing="8" Margin="12">
    <Button Content="Fit All" Click="FitAll" />
    <Button Content="Zoom to Selection" Click="ZoomSelection" />
    <Button Content="Reset" Click="Reset" />
    <TextBlock Text="{Binding Zoom, StringFormat='Zoom: {0:P0}'}" />
  </StackPanel>
</Grid>
```

```csharp
private void FitAll(object? s, RoutedEventArgs e) => Canvas.ZoomToFit();

private void ZoomSelection(object? s, RoutedEventArgs e)
{
    if (Canvas.Graph is { SelectedNodes: { Count: > 0 } selected })
        Canvas.ZoomToNodes(selected);
}

private void Reset(object? s, RoutedEventArgs e)
{
    Canvas.ViewportZoom = 1.0;
    Canvas.ViewportOffset = new Point(0, 0);
}
```

## Gotchas

- **`ViewportOffset` is in screen pixels, not world units.** The transform is `screen = world * ViewportZoom + ViewportOffset`. Persisting an offset across sessions requires also persisting the zoom that was in effect — mixing them produces a view jumbled by the ratio.
- **`MinZoom == MaxZoom` is the only supported way to disable zoom.** There's no flag. The clamp effectively swallows wheel / pinch input because any value the user tries gets mapped back to the single allowed value.
- **`ZoomToNodes` needs measured nodes.** Call it after the first layout pass — calling it in a window constructor, before the canvas has measured anything, will see `Node.Width` / `Height` as zero and produce a zero-sized bounding box. Defer it to `Dispatcher.UIThread.Post(...)` or a button click.
- **Programmatic `ViewportZoom` is clamped the next time the user zooms, not when you assign it.** If your code assigns `ViewportZoom = 10` and `MaxZoom = 5`, the canvas displays at 10x until the user wheels once, at which point it snaps to 5. Assign within-range values from your own code to avoid the surprise.
- **`Space` conflicts with text editors.** If your canvas hosts an editable node template that captures `Space` for text input, the canvas won't see it while focus is on the child — you get natural, if accidental, delegation. If focus is on the canvas itself, `Space`+left-drag takes precedence.
- **Two-way bindings on `ViewportZoom` can loop** if your view model clamps on set without idempotency. Make sure `set => SetProperty(ref _zoom, Math.Clamp(value, ...))` short-circuits when the clamped value equals the current value.
- **Touch pad users may find pinch zoom aggressive.** Avalonia exposes pinch deltas that tend to be large. If your users complain, narrow `MaxZoom` rather than trying to intercept the gesture.

## See also

- [Bind ViewportZoom, ViewportOffset, and Selection](bind-viewport.md)
- [NodiumGraphCanvas control reference](../reference/canvas-control.md)
- [Keyboard shortcuts](keyboard-shortcuts.md)
- [Snap to grid](snap-to-grid.md)
