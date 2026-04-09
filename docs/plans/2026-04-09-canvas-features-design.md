# Canvas Features & Developer Experience Design

**Date:** 2026-04-09
**Status:** Approved
**Scope:** Canvas control features, visual defaults, developer API, extension model

## Architecture & Binding Model

### MVVM by Convention

NodiumGraph follows MVVM by convention: `Node` is the ViewModel base class. Consumers subclass it to add domain properties, then define DataTemplates for visual representation. The library provides the VM infrastructure (INPC, position, size, port provider); the consumer completes the pattern on both sides (their Model behind it, their DataTemplate in front of it).

### Graph-Centric Binding

The `Graph` object is the single source of truth. The canvas binds to it via a single `StyledProperty<Graph>`.

| What | Binding | Direction |
|---|---|---|
| `Graph` | `StyledProperty<Graph>` on canvas | OneWay in |
| `Node.X`, `Node.Y` | INPC on Node -> canvas repositions control | TwoWay (canvas writes back after drag) |
| `Node.Width`, `Node.Height` | Canvas measures rendered template -> writes to Node | OneWay out (internal set) |
| `Graph.Nodes` | `ObservableCollection` -> canvas adds/removes node controls | OneWay in |
| `Graph.Connections` | `ObservableCollection` -> canvas redraws styled paths | OneWay in |
| `Graph.SelectedNodes` | Canvas writes selection state -> Graph reflects it | TwoWay |
| Viewport (zoom, offset) | `StyledProperty` on canvas | TwoWay |

### Handler Wiring

All handlers are optional `StyledProperty`ies on the canvas. No handler = no behavior for that interaction ("report, don't decide"). The canvas never mutates domain state directly.

```xml
<nodium:NodiumGraphCanvas
    Graph="{Binding MyGraph}"
    NodeHandler="{Binding MyNodeHandler}"
    ConnectionHandler="{Binding MyConnectionHandler}" />
```

## Rendering Model

| Element | Rendering | Customization |
|---|---|---|
| **Node** | DataTemplate (real Avalonia control) | Full template per subclass |
| **Port** | Default: library-rendered circle at `Port.Position`; opt-in: `PortTemplate` | Template override |
| **Connection** | Always custom-drawn | `IConnectionRouter` (path) + `IConnectionStyle` (appearance) |
| **Grid** | Always custom-drawn | Properties (size, color, visibility) |

### Rendering Order (bottom to top)

1. Grid
2. Connections (custom-drawn, styled)
3. Connection drag preview
4. Group node backgrounds
5. Node controls (DataTemplate-driven)
6. Port visuals (default or templated)
7. Selection marquee
8. Minimap overlay
9. Validation feedback

## New Model Properties

| Property | On | Type | Purpose |
|---|---|---|---|
| `Title` | `Node` | `string` | Header text in default template. Default: `GetType().Name` |
| `IsSelected` | `Node` | `bool` | Selection highlight. Set by canvas |
| `Name` | `Port` | `string` | Label next to port visual in default template |
| `Flow` | `Port` | `PortFlow` (`Input` \| `Output`) | Semantic direction for validation. Does NOT drive positioning |

`IPortProvider` remains the source of truth for port existence and positioning. `Port.Position` determines where ports render relative to the node. `Port.Flow` is domain data for `IConnectionValidator` and consumer logic.

## Canvas Features

### Tier 1 -- Must Ship (Table Stakes)

| Feature | Behavior | Input |
|---|---|---|
| Pan | Infinite canvas scroll | Middle-drag, Space+left-drag |
| Zoom | Toward cursor, clamped min/max | Scroll wheel, pinch |
| Zoom to fit | Frame all nodes with padding | API call + optional button |
| Node drag | Single and multi-selection drag | Left-drag on node |
| Selection | Click = select one (clear others) | Left-click |
| Toggle select | Add/remove from selection | Ctrl+click |
| Marquee select | Rectangle sweep | Left-drag on empty canvas |
| Additive marquee | Add to existing selection | Ctrl+left-drag on empty canvas |
| Connection draw | Drag from port, preview line follows cursor | Left-drag from port |
| Connection validation | Green/red feedback during drag | Visual on hover over target port |
| Delete | Report selected nodes+connections to handler | Delete key |
| Deselect | Clear selection | Escape, click empty canvas |

### Tier 2 -- Target Release

| Feature | Behavior | Notes |
|---|---|---|
| Snap to grid | Node positions round to grid increment | Toggleable, configurable grid size |
| Visible grid | Dot or line grid, scales with zoom | Toggleable, styled |
| Minimap | Small overview panel, clickable to navigate | Corner overlay, shows node rectangles + viewport rect |
| Auto-pan | Canvas scrolls when dragging near edges | During node drag and connection drag |
| Router presets | Built-in `IConnectionRouter` implementations | `StraightRouter`, `BezierRouter`, `StepRouter` |
| Keyboard shortcuts | Delete, Ctrl+A (select all), Escape (deselect/cancel) | Configurable key bindings |
| Connection cutting | Drag line through connections to sever them | Alt+left-drag (reports to `IConnectionHandler`) |
| Grouping node | Special node type containing other nodes | Library provides `GroupNode : Node` base. Children move with group |
| Comment node | Non-functional annotation node | Library provides `CommentNode : Node` base |

### Tier 3 -- Aspirational (No Touch Gestures)

| Feature | Behavior | Notes |
|---|---|---|
| Alignment tools | Align left/right/top/bottom/center, distribute evenly | API methods, consumer wires to UI |
| Copy/paste subgraphs | Serialize selection -> deserialize at cursor | Consumer implements via handler; library provides selection snapshot |
| Layout algorithm hook | `ILayoutEngine` interface: takes graph, returns node positions | Consumer plugs in Dagre/ELK/force-directed |
| Node search/filter | Dim/hide nodes not matching criteria | `FilterPredicate` property on canvas |
| Reroute points | Tiny passthrough nodes for tidying connection paths | `RerouteNode : Node` with single in/out |
| Collapse/expand | Toggle node between compact and full | `Node.IsCollapsed` property, canvas swaps template |
| Connection labels | Text overlay on connection midpoint | Lightweight mechanism, not full template |
| Decorators layer | Non-interactive overlay (badges, status icons) | Rendered above connections, below selection marquee |

## Default Visual Design

### Default Node Template (Header + Body)

The library ships a default node template used when the consumer provides no DataTemplate.

```
+----------------------------+
|  * Node Title              |  <- Node.Title, header with accent background
+----------------------------+
| o PortName     PortName o  |  <- Ports from IPortProvider.Ports at Port.Position
|                            |
|      (body content)        |  <- ContentPresenter for subclass content
|                            |
+----------------------------+
```

Port visuals render at their `Port.Position` (relative to node). The default template does not impose left/right layout -- positions are the consumer's business via `IPortProvider`.

### Template Override Levels

| Level | Consumer defines | Library provides |
|---|---|---|
| Body only (recommended) | Content inside node body area | Header, ports, border, selection highlight |
| Full node | Entire node visual from scratch | Nothing -- consumer takes full control |

### Default Port Visual

- Small filled circle (8px diameter)
- Default color: neutral gray
- Hovered during connection drag: green (valid) or red (invalid) based on `IConnectionValidator`
- Consumer overrides via `PortTemplate` on canvas

### Default Connection Style

- Bezier curve (`BezierRouter` default)
- 2px stroke, semi-transparent gray
- Selected/hovered: thicker, accent color

### Default Grid

- Dot grid pattern at regular intervals
- Subtle, theme-derived color
- Scales with zoom (secondary grid appears when zoomed out)
- Toggleable via `ShowGrid`

### Theme Integration

Colors derive from the active Avalonia theme (Fluent light/dark) so the library looks native without consumer configuration.

| Element | Source |
|---|---|
| Canvas background | `SystemChromeLowColor` or similar theme brush |
| Node header | Accent color (tintable via optional `Node.AccentColor`) |
| Node body | `SystemAltHighColor` (surface) |
| Node border (selected) | `SystemAccentColor` |
| Port default | `SystemBaseMediumColor` |
| Port valid hover | Green semantic color |
| Port invalid hover | Red semantic color |
| Connection | `SystemBaseMediumLowColor` |
| Grid dots | `SystemBaseLowColor` |

## Developer API Surface

### NodiumGraphCanvas -- StyledProperties

```
Graph                    : Graph
-- Viewport --
ViewportZoom             : double          (TwoWay, default 1.0)
ViewportOffset           : Point           (TwoWay, default 0,0)
MinZoom                  : double          (default 0.1)
MaxZoom                  : double          (default 5.0)
-- Grid --
ShowGrid                 : bool            (default true)
GridSize                 : double          (default 20.0)
SnapToGrid               : bool            (default false)
-- Templates --
NodeTemplate             : IDataTemplate?  (null = use default)
PortTemplate             : IDataTemplate?  (null = use default)
-- Connections --
DefaultConnectionStyle   : IConnectionStyle (default: BezierRouter + gray 2px)
ConnectionRouter         : IConnectionRouter (default: BezierRouter)
-- Handlers (all optional) --
NodeHandler              : INodeInteractionHandler?
ConnectionHandler        : IConnectionHandler?
SelectionHandler         : ISelectionHandler?
CanvasHandler            : ICanvasInteractionHandler?
ConnectionValidator      : IConnectionValidator?
-- Minimap --
ShowMinimap              : bool            (default false)
MinimapPosition          : MinimapPosition (BottomRight, BottomLeft, TopRight, TopLeft)
```

### Public Methods

```csharp
void ZoomToFit(double padding = 50.0)
void ZoomToNodes(IEnumerable<Node> nodes, double padding = 50.0)
void CenterOnNode(Node node)
void SelectAll()
void DeleteSelected()  // reports to handlers, does not mutate directly
```

### Built-in Router Implementations

| Class | Path |
|---|---|
| `StraightRouter` | Direct line: source -> target |
| `BezierRouter` | Cubic bezier with automatic control points |
| `StepRouter` | Orthogonal right-angle segments (Manhattan routing) |

### Progressive Customization Layers

| Layer | What you touch | What you get |
|---|---|---|
| 0. Zero config | Just bind `Graph` | Default everything |
| 1. Styling | Canvas properties | Grid, snap, zoom, connection style |
| 2. Behavior | Handler properties | Domain logic for interactions |
| 3. Validation | Strategy properties | Connection rules, routing |
| 4. Node templates | DataTemplates | Custom visuals per `Node` subclass |
| 5. Port template | `PortTemplate` | Custom port visuals globally |
| 6. Full control | Subclass everything | Custom Node, Port, Connection subclasses |

## Extension Model

### Pattern: Subclass + Implement + Template

```csharp
// 1. Subclass the model
public class ImageNode : Node
{
    public Bitmap? Preview { get; set; }
}

// 2. Implement a handler
public class MyConnectionHandler : IConnectionHandler
{
    public Result<Connection> OnConnectionRequested(Port source, Port target)
    {
        var connection = new Connection(source, target);
        _graph.AddConnection(connection);
        return connection;
    }

    public void OnConnectionDeleteRequested(Connection connection)
    {
        _graph.RemoveConnection(connection);
    }
}

// 3. Define a template (XAML)
// <DataTemplate DataType="local:ImageNode">
//     <Image Source="{Binding Preview}" MaxHeight="100" />
// </DataTemplate>
```

No registration, no plugin system. Avalonia's DataTemplate resolution handles type-to-template mapping.

### Template Resolution Order

1. Canvas `NodeTemplate` property (explicit selector)
2. Avalonia implicit DataTemplate resolution (by type)
3. Library default template (fallback)

### Undo/Redo Integration

The library does not implement undo. Handler interfaces provide before/after state for the consumer to record:

- `OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves)` -- `NodeMoveInfo` has `OldPosition` and `NewPosition`
- `OnConnectionRequested` / `OnConnectionDeleteRequested` -- consumer records add/remove
- `OnDeleteRequested(nodes, connections)` -- consumer records deletion

The library reports. The consumer records. Clean separation.
