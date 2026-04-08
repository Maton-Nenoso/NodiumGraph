# NodiumGraph — Design Document

**Date:** 2026-04-08
**Status:** Draft
**Author:** Stefan Maton

## Overview

NodiumGraph is an open-source graph editor library for Avalonia. It provides an interactive canvas with pan, zoom, node dragging, selection, and connection drawing — the primitives every node-based editor needs — while leaving domain logic, styling, and undo/redo to the consumer.

The library targets universal use: visual programming, workflow editors, data pipelines, engineering schematics, and any application that needs a node-and-connection diagram.

### Design Principles

1. **Interfaces over opinions** — Define clean abstractions for nodes, ports, connections, routing, and styling. Ship minimal defaults. Let consumers plug in their own.
2. **Hybrid rendering** — Nodes are real Avalonia controls (DataTemplate-driven). Connections, grid, and canvas chrome are custom-rendered for performance.
3. **Report, don't decide** — The library reports interactions (node moved, connection requested, delete pressed) via focused handler interfaces. The consumer decides what to do. The library never mutates domain state directly.
4. **Small surface area** — Ship the primitives. Resist feature creep. Routing algorithms, layout engines, and serialization live outside the library.

## Package Structure

```
NodiumGraph.Core       — Interfaces, base classes, enums. Zero Avalonia dependency.
NodiumGraph.Avalonia   — The canvas control, rendering, interaction handling.
```

`NodiumGraph.Core` exists so consumers can build and test ViewModels without referencing Avalonia.

## Core Model (`NodiumGraph.Core`)

### Nodes

```csharp
public interface INode
{
    Guid Id { get; }
    double X { get; set; }
    double Y { get; set; }
    double Width { get; }
    double Height { get; }
}
```

Nodes are the consumer's objects. The library reads position and size for layout, hit-testing, and connection routing. Width and Height are read-only from the library's perspective — the consumer's DataTemplate determines the visual size, and the library measures the rendered control to obtain these values.

### Ports

```csharp
public interface IPort
{
    Guid Id { get; }
    INode Owner { get; }
    Point Position { get; }
}
```

`Position` is relative to the owning node's top-left corner. The library computes the absolute position from the node's X/Y plus the port's relative offset.

Every connection goes through a port. There are two modes, determined per node type:

- **Fixed ports** — The node implements `IPortProvider`. The library renders port visuals at the declared positions and constrains connections to those ports.
- **Dynamic ports** — The node does not implement `IPortProvider`. When a user drags a connection to the node, the library creates a port at the boundary intersection point. The consumer receives the created port in the connection event.

A node is either port-provider or dynamic — no mixing. This keeps the interaction model predictable.

### Port Provider

```csharp
public interface IPortProvider
{
    IReadOnlyList<IPort> GetPorts();
}
```

Implemented by `INode` types that expose fixed connection points. The library queries this when rendering port visuals and when resolving connection targets during drag.

### Connections

```csharp
public interface IConnection
{
    Guid Id { get; }
    IPort SourcePort { get; }
    IPort TargetPort { get; }
}
```

Connections always reference ports, never nodes directly. For dynamic-port nodes this is transparent — the library creates the port, the consumer receives it in the connection handler.

### Connection Validation

```csharp
public interface IConnectionValidator
{
    bool CanConnect(IPort source, IPort target);
}
```

Called during connection drag to determine whether a hover target is valid. The library shows visual feedback (accept/reject indicator) based on the result. If no validator is registered, all connections are allowed.

### Connection Routing

```csharp
public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(IPort source, IPort target);
}
```

Transforms a source-target pair into a series of points that define the connection path. The library draws line segments through these points.

**Default implementation:** Returns `[source.AbsolutePosition, target.AbsolutePosition]` — a straight line.

Consumers can implement orthogonal routing, bezier curves, or any custom path. The router is replaceable per canvas instance.

### Connection Styling

```csharp
public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}
```

Applied per connection. The consumer provides a style via a property on their `IConnection` implementation or via a style selector callback on the canvas.

## Interaction Handlers

The library reports user interactions through focused interfaces. Consumers implement only the ones they need. The library checks at runtime which interfaces are provided.

### Node Interactions

```csharp
public interface INodeInteractionHandler
{
    /// Called after one or more nodes finish a drag move.
    /// Includes old and new positions for undo support.
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);

    /// Called when the user requests deletion (Delete key or other trigger).
    /// Consumer decides whether to actually remove the items.
    void OnDeleteRequested(IReadOnlyList<INode> nodes, IReadOnlyList<IConnection> connections);

    /// Called on double-click of a node.
    void OnNodeDoubleClicked(INode node);
}

public record NodeMoveInfo(INode Node, Point OldPosition, Point NewPosition);
```

### Connection Interactions

```csharp
public interface IConnectionHandler
{
    /// Called when the user completes a connection drag.
    /// Consumer creates the domain connection and returns it,
    /// or returns null to reject.
    IConnection? OnConnectionRequested(IPort source, IPort target);

    /// Called when a connection deletion is requested.
    void OnConnectionDeleteRequested(IConnection connection);
}
```

### Selection

```csharp
public interface ISelectionHandler
{
    /// Called when the set of selected nodes changes.
    void OnSelectionChanged(IReadOnlyList<INode> selectedNodes);
}
```

### Canvas Interactions

```csharp
public interface ICanvasInteractionHandler
{
    /// Called on double-click of empty canvas space.
    void OnCanvasDoubleClicked(Point worldPosition);

    /// Called when an external item is dropped onto the canvas (drag from toolbox).
    void OnCanvasDropped(Point worldPosition, object data);
}
```

### Registration

Handlers are set as properties on the canvas control:

```csharp
public NodiumGraphCanvas
{
    INodeInteractionHandler? NodeHandler { get; set; }
    IConnectionHandler? ConnectionHandler { get; set; }
    ISelectionHandler? SelectionHandler { get; set; }
    ICanvasInteractionHandler? CanvasHandler { get; set; }
    IConnectionValidator? ConnectionValidator { get; set; }
    IConnectionRouter? ConnectionRouter { get; set; }
}
```

All nullable — the library functions with sensible defaults when handlers are not provided.

## Canvas Control (`NodiumGraph.Avalonia`)

### `NodiumGraphCanvas`

The primary control. A `TemplatedControl` that hosts:

- An infinite canvas in world coordinates
- Node controls rendered via consumer-provided `DataTemplate`
- Custom-rendered connections, grid, and selection rectangle

### Bindable Properties

| Property | Type | Mode | Purpose |
|----------|------|------|---------|
| `Nodes` | `IEnumerable<INode>` | OneWay | The node collection to display |
| `Connections` | `IEnumerable<IConnection>` | OneWay | The connection collection to display |
| `SelectedNodes` | `IList<INode>` | TwoWay | Currently selected nodes |
| `ViewportZoom` | `double` | TwoWay | Current zoom level |
| `ViewportOffset` | `Point` | TwoWay | Current pan offset in world coordinates |
| `MinZoom` | `double` | OneWay | Minimum zoom level (default: 0.1) |
| `MaxZoom` | `double` | OneWay | Maximum zoom level (default: 3.0) |
| `SnapToGrid` | `bool` | OneWay | Enable grid snapping during drag |
| `GridSize` | `double` | OneWay | Grid cell size in world units |
| `ShowGrid` | `bool` | OneWay | Render background grid |
| `NodeTemplate` | `IDataTemplate` | OneWay | DataTemplate for node rendering |
| `ConnectionStyleSelector` | `Func<IConnection, IConnectionStyle>?` | OneWay | Per-connection style resolution |

### Built-in Interactions

**Pan:**
- Middle-mouse drag
- Space + left-mouse drag

**Zoom:**
- Scroll wheel (zooms toward cursor)
- Pinch gesture (touch devices)
- Bindable `ViewportZoom` for external controls (buttons, slider)

**Zoom-to-fit:**
- `ZoomToFit()` method — calculates bounding box of all nodes and adjusts viewport

**Node dragging:**
- Left-mouse drag on a node
- Multi-drag: all selected nodes move together
- Optional snap-to-grid (nearest grid point)
- Reports `NodesMoved` to handler after drag completes (not during)

**Selection:**
- Left-click on node → select (clears previous)
- Ctrl + left-click → toggle selection
- Left-drag on empty canvas → marquee rectangle selection
- Ctrl + marquee → additive selection

**Connection drawing:**
- Left-drag from a port (on `IPortProvider` nodes) or from a node (dynamic port nodes) starts a connection
- Hover over valid targets shows accept/reject feedback via `IConnectionValidator`
- Release on valid target calls `IConnectionHandler.OnConnectionRequested`
- Release on empty space cancels

**Hit-testing:**
- Nodes: bounding box from measured control size
- Ports: circular hit area around port position
- Connections: proximity test against rendered line segments

### Positioning API

```csharp
public class NodiumGraphCanvas
{
    /// Moves a single node without raising NodesMoved.
    /// Use for layout algorithms, undo/redo restore.
    void SetNodePosition(INode node, double x, double y);

    /// Moves multiple nodes in a batch without raising NodesMoved.
    /// Optionally animates the transition.
    void SetNodePositions(
        IReadOnlyDictionary<INode, Point> positions,
        bool animate = false);
}
```

These methods update node positions programmatically (layout, undo). They suppress `NodesMoved` events to avoid feedback loops. The `animate` flag enables a smooth transition (useful for layout changes).

### Rendering Architecture

**Nodes** are hosted in an internal `ItemsControl`-like mechanism on the canvas. Each `INode` gets wrapped in a container that:
- Positions itself at the node's world coordinates, transformed by the current viewport
- Renders the consumer's `NodeTemplate` DataTemplate
- Handles drag interaction and selection visuals
- Measures the rendered size and writes it back to `INode.Width`/`Height`

**Connections** are rendered in a custom `Render()` override below the node layer:
- For each `IConnection`, query the `IConnectionRouter` for the point list
- Draw line segments through the points using the connection's `IConnectionStyle`
- Selected connections render with a highlight

**Grid** is rendered as the bottommost layer:
- Dot grid or line grid (configurable)
- Scales and offsets with the viewport
- Only renders visible cells for performance

**Selection rectangle** is rendered as an overlay above everything during marquee drag.

### Rendering Order (bottom to top)

1. Grid background
2. Connections
3. Active connection drag preview (dashed line following cursor)
4. Node containers (with consumer DataTemplates)
5. Port visuals (circles at port positions, rendered on top of nodes)
6. Selection marquee rectangle
7. Connection validation feedback (accept/reject indicator at cursor)

## Consumer Usage Example

### Minimal (node-to-node, no ports)

```xml
<nodium:NodiumGraphCanvas
    Nodes="{Binding MyNodes}"
    Connections="{Binding MyConnections}"
    SelectedNodes="{Binding SelectedNodes, Mode=TwoWay}"
    ViewportZoom="{Binding Zoom, Mode=TwoWay}">
    <nodium:NodiumGraphCanvas.NodeTemplate>
        <DataTemplate DataType="vm:MyNodeViewModel">
            <Border Background="White" Padding="8" CornerRadius="4">
                <TextBlock Text="{Binding Name}"/>
            </Border>
        </DataTemplate>
    </nodium:NodiumGraphCanvas.NodeTemplate>
</nodium:NodiumGraphCanvas>
```

### Port-based (engineering schematic)

```csharp
public class ComponentNodeVm : INode, IPortProvider
{
    public Guid Id { get; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; }
    public double Height { get; }

    public IReadOnlyList<IPort> GetPorts() => _ports;
}
```

The library renders port visuals automatically for `IPortProvider` nodes and constrains connection drag targets to declared ports.

### Handler registration

```xml
<nodium:NodiumGraphCanvas
    NodeHandler="{Binding}"
    ConnectionHandler="{Binding}"
    SelectionHandler="{Binding}"
    ConnectionValidator="{Binding MyValidator}"/>
```

Or in code-behind:

```csharp
canvas.NodeHandler = myNodeHandler;
canvas.ConnectionHandler = myConnectionHandler;
```

## What NodiumGraph Does NOT Do

These are explicitly out of scope — consumers handle them:

- **Undo/redo** — Use handler events to build undo commands in your app's undo system
- **Layout algorithms** — Compute positions externally, apply via `SetNodePositions()`
- **Serialization** — The library has no save/load; it renders whatever objects you give it
- **Context menus** — Attach via standard Avalonia `ContextMenu` on your DataTemplates
- **Keyboard shortcuts** — Handle at window level, call library APIs as needed
- **Search/filter/overlays** — Put these in your node DataTemplate; the library renders whatever you provide
- **Connection validation logic** — Implement `IConnectionValidator` with your domain rules
- **Node content/appearance** — Entirely consumer-defined via `DataTemplate`

## Non-Functional Requirements

- **Performance target:** Smooth pan/zoom with 500+ nodes and 1000+ connections
- **No third-party dependencies** beyond Avalonia itself (no QuikGraph, no ReactiveUI)
- **AOT-compatible** — No reflection in hot paths; consumers use compiled bindings
- **Avalonia 11.x and 12.x support** — Target the latest stable; consider multi-targeting if feasible
- **Unit testable** — Core interfaces testable without Avalonia; canvas interactions testable via Avalonia headless

## Open Questions

1. **Port visuals** — Should the library render port indicators (small circles) by default, or should port rendering be consumer-controlled via a `PortTemplate`? Leaning toward a default circle with an optional `PortTemplate` override.
2. **Multi-select connections** — Should connections be individually selectable, or only nodes? Current design supports both, but simpler editors may want node-only selection.
3. **Touch/stylus** — How much touch interaction should v1 support beyond pinch-zoom? Defer to post-v1?
4. **Minimap** — Common feature in graph editors. Worth including in v1, or defer? If included, it should be a separate control that binds to the canvas state, not embedded.
