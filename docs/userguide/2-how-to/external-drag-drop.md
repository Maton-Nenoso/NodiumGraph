# Handle External Drag-Drop onto the Canvas

## Goal

Let the user drag something from outside the canvas — a palette item, a file from Explorer, a row from another list — and drop it on the canvas to create a new node at the drop location.

## Prerequisites

- You already host `NodiumGraphCanvas` and have a `Graph` assigned. See [Host the Canvas](host-canvas.md).
- You're familiar with Avalonia's `DragDrop` primitive, specifically `DragDrop.DoDragDrop` and `IDataTransfer` (Avalonia 12's replacement for `IDataObject`).

## Steps

### 1. Understand what the canvas gives you

`NodiumGraphCanvas` opts into `DragDrop.AllowDrop` automatically — you do **not** have to set `DragDrop.AllowDrop="True"` in your AXAML. When a drop completes on the canvas, it:

1. Reads the drop position relative to itself.
2. Converts it to world coordinates using the current `ViewportZoom` and `ViewportOffset` — so pan and zoom are already undone.
3. Calls `ICanvasInteractionHandler.OnCanvasDropped(worldPosition, data)`, passing Avalonia's `IDataTransfer` straight through.
4. Marks the drag event as handled.

If `CanvasHandler` is `null`, the drop is silently ignored. That's the only job the canvas does for you — the actual "what does this data represent?" decoding is yours.

### 2. Define your drag data format

Use a custom format string so you don't accidentally handle generic `Text` drops from somewhere else:

```csharp
public static class GraphDragFormats
{
    public const string NodeBlueprint = "nodiumgraph/node-blueprint";
}
```

On the *source* side (wherever your palette lives), start the drag with a concrete payload:

```csharp
private async void OnPalettePointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (sender is not Control source) return;
    if (source.DataContext is not NodeBlueprint blueprint) return;

    var data = new DataObject();
    data.Set(GraphDragFormats.NodeBlueprint, blueprint);

    await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
}
```

`NodeBlueprint` here is your own plain C# class — maybe `record NodeBlueprint(string Kind, string Title)`. The canvas does not care what it is; it only hands it back to you untouched.

### 3. Implement `ICanvasInteractionHandler.OnCanvasDropped`

```csharp
public sealed class CanvasDropHandler(Graph graph) : ICanvasInteractionHandler
{
    public void OnCanvasDoubleClicked(Point worldPosition) { /* unrelated */ }

    public void OnCanvasDropped(Point worldPosition, IDataTransfer data)
    {
        if (!data.Contains(GraphDragFormats.NodeBlueprint)) return;

        if (data.Get(GraphDragFormats.NodeBlueprint) is not NodeBlueprint blueprint) return;

        var node = CreateNode(blueprint, worldPosition);
        graph.AddNode(node);
    }

    private static Node CreateNode(NodeBlueprint blueprint, Point worldPosition)
    {
        return new MathNode
        {
            Title = blueprint.Title,
            X = worldPosition.X,
            Y = worldPosition.Y,
            PortProvider = BuildPorts(blueprint),
        };
    }

    private static IPortProvider BuildPorts(NodeBlueprint blueprint) { /* ... */ }
}
```

Wire it up once on the canvas:

```csharp
Canvas.CanvasHandler = new CanvasDropHandler(graph);
```

### 4. Dropping files from Explorer / Finder

File drops use Avalonia's standard `DataFormats.Files` format. Decode them inside the same handler:

```csharp
public void OnCanvasDropped(Point worldPosition, IDataTransfer data)
{
    if (data.Contains(DataFormats.Files))
    {
        var files = data.GetFiles();
        if (files is null) return;

        var offset = Point.Empty;
        foreach (var file in files)
        {
            var node = new FileNode { Title = file.Name, Path = file.Path.LocalPath };
            node.X = worldPosition.X + offset.X;
            node.Y = worldPosition.Y + offset.Y;
            graph.AddNode(node);
            offset = new Point(offset.X + 20, offset.Y + 20);
        }
        return;
    }

    // ...fall through to your own formats...
}
```

## Full code

```csharp
using Avalonia.Input;
using Avalonia.Input.DragDrop;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class CanvasDropHandler(Graph graph) : ICanvasInteractionHandler
{
    public void OnCanvasDoubleClicked(Point worldPosition)
    {
        // Spawn a blank node on empty-canvas double-click.
        graph.AddNode(new MathNode { X = worldPosition.X, Y = worldPosition.Y });
    }

    public void OnCanvasDropped(Point worldPosition, IDataTransfer data)
    {
        if (data.Contains(GraphDragFormats.NodeBlueprint) &&
            data.Get(GraphDragFormats.NodeBlueprint) is NodeBlueprint blueprint)
        {
            var node = new MathNode
            {
                Title = blueprint.Title,
                X = worldPosition.X,
                Y = worldPosition.Y,
            };
            graph.AddNode(node);
        }
    }
}

public static class GraphDragFormats
{
    public const string NodeBlueprint = "nodiumgraph/node-blueprint";
}

public sealed record NodeBlueprint(string Kind, string Title);
```

## Gotchas

- **`AllowDrop` is already `true`.** Setting it again in AXAML is harmless but redundant. Do not set `DragDrop.AllowDrop="False"` on the canvas — that silently breaks this whole flow.
- **The canvas ignores drops if `CanvasHandler` is `null`.** You will not get an exception; nothing will happen. Wire the handler before telling users the feature works.
- **`worldPosition` is already in world space.** Don't re-apply the viewport transform. If you log drops and the numbers look wrong, you are probably double-transforming.
- **`IDataTransfer` replaces `IDataObject` in Avalonia 12.** Old samples that call `data.GetDataPresent(...)` / `data.GetData(...)` need to move to `Contains(...)` / `Get(...)`. `GetFiles()` replaces `GetFileNames()`.
- **`OnCanvasDropped` does not fire for drops on nodes or ports.** Node and port hit targets consume the drop before it reaches canvas-level handling. If you need to drop *onto* a specific node, handle the drop inside that node's `DataTemplate` — the canvas is not the right layer for it.
- **Drag effects are the source's responsibility.** The canvas does not vet the effect (`Copy` / `Move` / `Link`). Pick the right one when you call `DragDrop.DoDragDrop`.
- **Drops are UI-thread only.** Avoid blocking work inside `OnCanvasDropped`; if you need to read a file or hit the network to resolve the dropped item, start the async work and add the node when it completes — the canvas does not wait for you.

## See also

- [Handler interfaces reference](../3-reference/handlers.md)
- [NodiumGraphCanvas control reference](../3-reference/canvas-control.md)
- [Host the Canvas](host-canvas.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
