# Enable Snap-to-Grid

## Goal

Make user-initiated node drags snap to the canvas grid when they complete, so nodes line up neatly without manual fiddling. Optionally show a "ghost" preview of the snapped position during the drag so users see where the node will land.

## Prerequisites

- You already host `NodiumGraphCanvas` with `ShowGrid="True"`. See [Host the Canvas](host-canvas.md).
- You know you want snap-on-completion behaviour, not snap-on-every-pointer-move. NodiumGraph snaps once, at the end of each drag — by design.

## Steps

### 1. Understand the three properties

Snap-to-grid is controlled by three styled properties on `NodiumGraphCanvas`:

| Property | Type | Default | Role |
|---|---|---|---|
| `GridSize` | `double` | `20.0` | Spacing between minor grid lines, in world units. Also the snap increment. |
| `SnapToGrid` | `bool` | `false` | When `true`, dragged nodes snap to `GridSize` increments on drag completion. |
| `ShowSnapGhost` | `bool` | `false` | Only meaningful when `SnapToGrid` is `true`. Renders a ghost outline at the snapped position while the drag is in progress. |

`GridSize` is shared with the grid renderer, so the snap interval always matches the visible grid — you cannot snap to a different size than you're drawing.

### 2. Enable snap

```xml
<ng:NodiumGraphCanvas x:Name="Canvas"
                      ShowGrid="True"
                      GridSize="20"
                      SnapToGrid="True"
                      ShowSnapGhost="True" />
```

Or from code:

```csharp
Canvas.GridSize = 20;
Canvas.SnapToGrid = true;
Canvas.ShowSnapGhost = true;
```

No other wiring is needed. Every drag that completes on a selected node — single or multi-selection — now lands on a `GridSize`-aligned position.

### 3. When the snap happens

The snap is applied **once**, at drag completion, before `INodeInteractionHandler.OnNodesMoved` fires. This means:

- During the drag, the visual node follows the pointer exactly. If `ShowSnapGhost` is on, you see a ghost outline showing where the drop would land; the real visual doesn't jump.
- On release, the node's `X` / `Y` snap to the nearest multiple of `GridSize`, and your `NodeHandler.OnNodesMoved(...)` is called with `NodeMoveInfo.NewPosition` already containing the snapped values.
- Your undo/redo code (see [Handle node moves for undo](handle-node-moves-undo.md)) does not need to re-snap — the positions it receives are already aligned.

This is intentional: snapping during the drag makes fine movement feel sticky and low-resolution, while snap-on-release keeps the pointer feeling smooth while still giving you a clean final layout.

### 4. Change the grid size without breaking alignment

`GridSize` is live — changing it reconfigures both the visible grid and the snap step on the next render. Existing nodes are not re-snapped automatically; they stay at whatever position they had. If you change the grid size mid-session and want existing nodes to realign, do it manually:

```csharp
Canvas.GridSize = 40;

// Optional: realign every existing node to the new grid.
if (Canvas.Graph is { } graph)
{
    foreach (var node in graph.Nodes)
    {
        node.X = Math.Round(node.X / Canvas.GridSize) * Canvas.GridSize;
        node.Y = Math.Round(node.Y / Canvas.GridSize) * Canvas.GridSize;
    }
}
```

You typically don't want to do this behind the user's back — it shifts everything at once and can't be undone through a normal user drag — so only run it in response to an explicit "snap existing layout to new grid" command.

### 5. Expose the toggle in your UI

`SnapToGrid` is a standard `StyledProperty`, so you can two-way bind it to a checkbox or toggle control in your view model:

```xml
<CheckBox IsChecked="{Binding SnapToGrid, Mode=TwoWay}" Content="Snap to grid" />
<ng:NodiumGraphCanvas SnapToGrid="{Binding SnapToGrid}"
                      ShowSnapGhost="{Binding SnapToGrid}" />
```

Binding `ShowSnapGhost` to the same source is a common pattern — the ghost is only useful when snap is on anyway.

### 6. Snap without a visible grid

Snap and grid rendering are independent. You can hide the grid and still snap:

```xml
<ng:NodiumGraphCanvas ShowGrid="False"
                      GridSize="20"
                      SnapToGrid="True" />
```

The canvas will still snap drops to multiples of `GridSize = 20`; users just won't see the grid dots while they work. This is sometimes preferred for clean layouts where the grid itself would be visual noise.

## Full code

```xml
<ng:NodiumGraphCanvas x:Name="Canvas"
                      Background="#F1F5F9"
                      ShowGrid="True"
                      GridStyle="Dots"
                      GridSize="20"
                      MajorGridInterval="5"
                      SnapToGrid="{Binding SnapEnabled}"
                      ShowSnapGhost="{Binding SnapEnabled}" />
```

```csharp
// ViewModel: plain bindable bool drives both snap behaviour and the ghost preview.
public sealed class MainViewModel : ObservableObject
{
    private bool _snapEnabled = true;
    public bool SnapEnabled
    {
        get => _snapEnabled;
        set => SetProperty(ref _snapEnabled, value);
    }
}
```

## Gotchas

- **Snap happens on drag *completion*, not during.** If you expected the node to click into grid cells while moving, that's not how NodiumGraph works. Turn on `ShowSnapGhost` so users see the intended landing spot during the drag.
- **`GridSize` is shared between rendering and snapping.** You cannot draw a 20-unit grid and snap at 10. If you need a different snap increment, you either change `GridSize` (and accept the visible grid matches) or write your own post-drag normalisation in `INodeInteractionHandler.OnNodesMoved`.
- **`NodeMoveInfo.NewPosition` is already snapped.** Don't re-snap in your handler — you'll compound rounding errors and might shift nodes by a full grid cell over multiple drags.
- **Snap does not retroactively align existing nodes.** Toggling `SnapToGrid = true` does not reposition anything that was already on the canvas at unaligned coordinates. Only new drags are affected. Run the manual realign snippet in step 4 if you need existing nodes to catch up.
- **Snap does not apply to programmatic writes.** Setting `node.X = 17.3` from your own code leaves the value at `17.3`. The snap logic lives in the drag-completion path, not in the `Node.X` setter.
- **Multi-selection drags snap per-node.** Each selected node snaps to the nearest grid cell independently, so a group whose nodes were on mismatched sub-grid offsets may change its internal spacing slightly after a snap. If you need "preserve relative offsets, snap the group anchor", implement that in your handler before accepting the move.
- **`ShowSnapGhost` with `SnapToGrid = false` is a no-op.** The ghost only appears when there's something to ghost to.

## See also

- [Handle node moves for undo/redo](handle-node-moves-undo.md)
- [NodiumGraphCanvas control reference](../reference/canvas-control.md)
- [Configure pan and zoom gestures](configure-pan-zoom.md)
- [Theme the canvas](theme-canvas.md)
