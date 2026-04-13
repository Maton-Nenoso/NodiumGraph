# Add Keyboard Shortcuts

## Goal

Wire up keyboard shortcuts — Delete, Ctrl+A, Ctrl+F for fit-to-content, and whatever else your app needs — to the operations `NodiumGraphCanvas` already exposes as methods.

## Prerequisites

- You already host `NodiumGraphCanvas` with a `Graph` and, for Delete, a `NodeHandler` that implements `INodeInteractionHandler.OnDeleteRequested`. See [Host the Canvas](host-canvas.md) and [Handle node moves for undo](handle-node-moves-undo.md).
- You understand that **NodiumGraph has no built-in keyboard shortcuts**. This is a deliberate scope decision — see the [architecture explanation](../explanation/architecture.md) — and it means every shortcut you want is something *you* wire up.

## Steps

### 1. What the canvas exposes as methods

`NodiumGraphCanvas` has a small set of public methods that consumer code is expected to call in response to shortcuts:

| Method | What it does |
|---|---|
| `DeleteSelected()` | Calls `NodeHandler.OnDeleteRequested(selectedNodes, affectedConnections)`. The handler decides whether to actually remove anything. |
| `SelectAll()` | Adds every node in the graph to the selection and fires `ISelectionHandler.OnSelectionChanged`. |
| `ClearSelection()` | Drops every node from the selection and fires `ISelectionHandler.OnSelectionChanged`. |
| `SelectNode(Node node)` | Adds a specific node to the selection. |
| `ZoomToFit(double padding = 50)` | Frames every node in the visible area. |
| `ZoomToNodes(IEnumerable<Node> nodes, double padding = 50)` | Frames a subset. |

Everything you need for standard shortcuts is one of these calls. The canvas does not read the keyboard on your behalf.

### 2. The idiomatic path: `Window.KeyBindings`

The Avalonia way to add window-scoped shortcuts is `Window.KeyBindings`. Each entry binds a gesture to a `Command`, and — because the canvas methods are plain `void`-returning methods — you route through a view-model command or a code-behind event handler.

Code-behind is the lightest path if your app doesn't already use MVVM commanding:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"
        x:Class="MyApp.MainWindow">

  <Window.KeyBindings>
    <KeyBinding Gesture="Delete" Command="{Binding DeleteCommand}" />
    <KeyBinding Gesture="Back" Command="{Binding DeleteCommand}" />
    <KeyBinding Gesture="Ctrl+A" Command="{Binding SelectAllCommand}" />
    <KeyBinding Gesture="Escape" Command="{Binding ClearSelectionCommand}" />
    <KeyBinding Gesture="Ctrl+0" Command="{Binding FitAllCommand}" />
  </Window.KeyBindings>

  <ng:NodiumGraphCanvas x:Name="Canvas" />
</Window>
```

The commands themselves are trivial wrappers around the canvas methods:

```csharp
public sealed class MainViewModel : ObservableObject
{
    public NodiumGraphCanvas Canvas { get; set; } = null!;

    public ICommand DeleteCommand => new RelayCommand(() => Canvas.DeleteSelected());
    public ICommand SelectAllCommand => new RelayCommand(() => Canvas.SelectAll());
    public ICommand ClearSelectionCommand => new RelayCommand(() => Canvas.ClearSelection());
    public ICommand FitAllCommand => new RelayCommand(() => Canvas.ZoomToFit());
}
```

If you prefer code-behind over MVVM commanding, use an event-based approach instead:

```csharp
public MainWindow()
{
    AvaloniaXamlLoader.Load(this);

    KeyBindings.Add(new KeyBinding
    {
        Gesture = KeyGesture.Parse("Delete"),
        Command = new RelayCommand(Canvas.DeleteSelected),
    });

    KeyBindings.Add(new KeyBinding
    {
        Gesture = KeyGesture.Parse("Ctrl+A"),
        Command = new RelayCommand(Canvas.SelectAll),
    });
}
```

### 3. Delete flows through your handler, not through the canvas

`DeleteSelected()` does **not** mutate the graph directly — it calls `NodeHandler.OnDeleteRequested(nodes, connections)` and leaves the decision to you. That's the same pattern as every other user-triggered action in the library: report, don't decide.

If you haven't wired a `NodeHandler`, pressing Delete does nothing. Your handler must actually call `graph.RemoveNode(...)` / `graph.RemoveConnection(...)` for the deletion to be visible:

```csharp
public sealed class AppNodeHandler(Graph graph, UndoStack undo) : INodeInteractionHandler
{
    public void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections)
    {
        var undoEntry = new DeleteOp(nodes.ToList(), connections.ToList());
        foreach (var c in connections) graph.RemoveConnection(c);
        foreach (var n in nodes) graph.RemoveNode(n);
        undo.Push(undoEntry);
    }

    public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves) { /* ... */ }
    public void OnNodeDoubleClicked(Node node) { /* ... */ }
}
```

This separation means "Delete" can become "soft delete", "confirm dialog", or "delete + undo entry" without the library ever knowing.

### 4. Undo / redo belong to your app, not the library

There is no `Canvas.Undo()` / `Canvas.Redo()`. Bind `Ctrl+Z` / `Ctrl+Y` to your own undo stack:

```xml
<Window.KeyBindings>
  <KeyBinding Gesture="Ctrl+Z" Command="{Binding UndoCommand}" />
  <KeyBinding Gesture="Ctrl+Y" Command="{Binding RedoCommand}" />
  <KeyBinding Gesture="Ctrl+Shift+Z" Command="{Binding RedoCommand}" />
</Window.KeyBindings>
```

See [Handle node moves for undo](handle-node-moves-undo.md) for the rest of the undo/redo wiring.

### 5. Focus matters

`Window.KeyBindings` only fires when the window has focus and no focused child has already handled the key. If the canvas hosts an editable node template (a `TextBox` inside a DataTemplate, for example), that child will capture keys while it has focus — Delete will delete text inside the TextBox, not nodes on the canvas. This is usually what the user expects.

If you want shortcuts to fire even while a child has focus, move them to the `Window.Styles` level with a high-priority handler, or intercept `KeyDown` on the canvas itself and dispatch to the right method.

### 6. A focused user experience

A few well-chosen shortcuts go further than a crowded palette:

| Gesture | Action | Call |
|---|---|---|
| `Delete` / `Back` | Delete selection | `Canvas.DeleteSelected()` |
| `Ctrl+A` | Select all | `Canvas.SelectAll()` |
| `Escape` | Clear selection | `Canvas.ClearSelection()` |
| `Ctrl+0` | Fit all | `Canvas.ZoomToFit()` |
| `Ctrl+1` | Reset zoom | `Canvas.ViewportZoom = 1.0; Canvas.ViewportOffset = new Point(0, 0);` |
| `Ctrl+F` | Zoom to selection | `Canvas.ZoomToNodes(Canvas.Graph!.SelectedNodes)` |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo | your undo stack |

Everything on this list is consumer-side wiring — the library offers the building blocks, you decide which gestures your users see.

## Full code

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ng="clr-namespace:NodiumGraph.Controls;assembly=NodiumGraph"
        x:Class="MyApp.MainWindow">

  <Window.KeyBindings>
    <KeyBinding Gesture="Delete" Command="{Binding DeleteCommand}" />
    <KeyBinding Gesture="Back" Command="{Binding DeleteCommand}" />
    <KeyBinding Gesture="Ctrl+A" Command="{Binding SelectAllCommand}" />
    <KeyBinding Gesture="Escape" Command="{Binding ClearSelectionCommand}" />
    <KeyBinding Gesture="Ctrl+0" Command="{Binding FitAllCommand}" />
    <KeyBinding Gesture="Ctrl+F" Command="{Binding ZoomSelectionCommand}" />
    <KeyBinding Gesture="Ctrl+Z" Command="{Binding UndoCommand}" />
    <KeyBinding Gesture="Ctrl+Y" Command="{Binding RedoCommand}" />
  </Window.KeyBindings>

  <ng:NodiumGraphCanvas x:Name="Canvas" Graph="{Binding Graph}" />
</Window>
```

```csharp
public sealed class MainViewModel(NodiumGraphCanvas canvas, UndoStack undo) : ObservableObject
{
    public Graph Graph { get; } = new();

    public ICommand DeleteCommand { get; } = new RelayCommand(canvas.DeleteSelected);
    public ICommand SelectAllCommand { get; } = new RelayCommand(canvas.SelectAll);
    public ICommand ClearSelectionCommand { get; } = new RelayCommand(canvas.ClearSelection);
    public ICommand FitAllCommand { get; } = new RelayCommand(() => canvas.ZoomToFit());
    public ICommand ZoomSelectionCommand { get; } = new RelayCommand(() =>
    {
        if (canvas.Graph is { SelectedNodes.Count: > 0 } g)
            canvas.ZoomToNodes(g.SelectedNodes);
    });
    public ICommand UndoCommand { get; } = new RelayCommand(undo.Undo);
    public ICommand RedoCommand { get; } = new RelayCommand(undo.Redo);
}
```

## Gotchas

- **The library has no built-in shortcuts.** Not having wired any is *not* a bug. Every action a user can take with a key is something your code bound. This is deliberate — different apps want different gestures, and the library refuses to pick.
- **`DeleteSelected()` is a no-op without a `NodeHandler`.** It calls `NodeHandler.OnDeleteRequested(...)`, and a null handler means the call silently does nothing. If Delete seems broken, wire a handler first.
- **Focus competes with the canvas.** Text editors inside node templates capture keys while they have focus. If Delete deletes a character instead of a node, the user is focused on a text box, not the canvas — usually correct behaviour. Watch the `FocusManager` if you need to debug this.
- **`Window.KeyBindings` is window-scoped.** A child window or dialog opens without these bindings. If your app has multiple windows hosting canvases, either duplicate the bindings or centralise them on `Application`.
- **`KeyGesture.Parse` is strict.** `"Ctrl+Shift+F"` works; `"CTRL+F"` does not. Use the `KeyGesture` constructors if you want compile-time safety.
- **`Ctrl+A` vs. `Cmd+A` on macOS.** Avalonia normalises platform modifiers differently depending on the gesture source. If you target both platforms, test both — `Meta` and `Control` are not interchangeable in gestures.
- **Your canvas must have focus for `Ctrl+0` / `Ctrl+F` / etc. to fire** when those gestures aren't window-level. When in doubt, put them in `Window.KeyBindings` rather than on the canvas — window-level bindings fire regardless of focus inside the window.

## See also

- [Handler interfaces reference](../reference/handlers.md)
- [Handle node moves for undo/redo](handle-node-moves-undo.md)
- [Configure pan and zoom gestures](configure-pan-zoom.md)
- [Bind ViewportZoom, ViewportOffset, and Selection](bind-viewport.md)
- [Report, don't decide](../explanation/report-dont-decide.md)
