# Node Overhaul Design Spec

A comprehensive improvement to the NodiumGraph node system covering styling, header visibility, port positioning, collapse, and grid snap feedback.

## 1. NodeStyle & PortStyle

### NodeStyle

New class holding visual overrides for a node. Set per-node via `Node.Style`. All properties nullable — `null` means "use theme default." Implements `INotifyPropertyChanged`.

```
NodeStyle
  HeaderBackground  : IBrush?       // gradient, solid, image brush
  HeaderForeground  : IBrush?       // title text color
  BodyBackground    : IBrush?       // body area fill
  BorderBrush       : IBrush?       // outer border
  BorderThickness   : double?       // outer border width
  CornerRadius      : CornerRadius? // rounding
  Opacity           : double?       // overall node opacity (0.0-1.0)
```

### PortStyle

Same pattern for ports. Set per-port via `Port.Style`.

```
PortStyle
  Fill        : IBrush?    // port interior
  Stroke      : IBrush?    // port border
  StrokeWidth : double?    // port border thickness
  Shape       : PortShape? // Circle, Square, Diamond, Triangle
  Size        : double?    // diameter/side length
```

### PortShape enum

```
PortShape { Circle, Square, Diamond, Triangle }
```

### Resolution order

1. Per-instance style property (if non-null)
2. Theme resource (e.g., `NodeHeaderBrushKey`)
3. Hardcoded default

### Model changes

- `Node.Style` : `NodeStyle?` (default null)
- `Port.Style` : `PortStyle?` (default null)

IBrush covers solid colors, linear/radial/conic gradients, image brushes, visual brushes, and drawing brushes. Transparency is supported via ARGB color alpha and brush-level Opacity.

## 2. Header Toggle

Visual-only property controlling whether the default template renders the header bar.

### Model change

- `Node.ShowHeader` : `bool` (default `true`, INPC-enabled)
- `Title` property unchanged

### Default template behavior

- `ShowHeader == true`: current layout (colored header bar with title text + body area)
- `ShowHeader == false`: header bar hidden, node shows body area only. Height shrinks naturally.

### Interaction with NodeStyle

- `HeaderBackground` and `HeaderForeground` ignored when `ShowHeader == false`
- `BodyBackground`, `BorderBrush`, etc. still apply

### Interaction with collapse

| ShowHeader | IsCollapsed | Renders                                          |
|------------|-------------|--------------------------------------------------|
| true       | false       | Full node (header + body)                        |
| true       | true        | Header bar only, body hidden                     |
| false      | false       | Body only (no header)                            |
| false      | true        | Minimal pill/bar indicator with node border style |

### Template scope

`ShowHeader` is respected by the **built-in default Node template only**. The specialized built-in templates (`CommentNodeTemplate`, `GroupNodeTemplate`) do not have a header/body split and ignore it. Consumers using custom templates can bind to it if desired.

`IsCollapsed` has two layers — see Section 4 for full details:
- **Behavioral** (canvas-enforced, always applies): port hiding, connection blocking
- **Visual** (template-driven): how the node shrinks. Built-in templates handle this; custom templates should bind to `IsCollapsed` for visual consistency.

## 3. Port Positioning, Labels, and Node Shapes

### Angle-based positioning

Replaces fixed-coordinate positioning with an angle-based system. Ports specify an angle (0-360 degrees, 0 = top, clockwise). The system places the port at the intersection of that angle's ray from the node center with the node's boundary shape.

### Port INPC

`Port` currently has no `INotifyPropertyChanged` implementation. This overhaul adds it. `Port` gains INPC infrastructure (same `SetField<T>` pattern as `Node`) for the new properties: `Angle`, `Label`, `LabelPlacement`, `Style`. The existing `Position` property also becomes INPC-enabled so the canvas can react when port providers update positions.

Existing `Port` properties (`Id`, `Owner`, `Name`, `Flow`) remain immutable — no INPC needed for those.

### Port model changes

- `Port.Angle` : `double` (0 = top, clockwise, INPC)
- `Port.Label` : `string?` (display text, INPC)
- `Port.LabelPlacement` : `PortLabelPlacement?` (nullable = auto, INPC)
- `Port.Position` changes from `init` to `{ get; internal set; }` with INPC — port providers set it, consumers read it

### PortLabelPlacement enum

```
PortLabelPlacement { Left, Right, Above, Below }
```

### INodeShape interface

```csharp
public interface INodeShape
{
    Point GetBoundaryPoint(double angleDegrees, double width, double height);
}
```

Returns a point relative to node center. The canvas adds `Node.X + Width/2` and `Node.Y + Height/2` for absolute position.

`INodeShape` is used solely for port boundary math. It does not affect the visual rendering of the node — that remains the DataTemplate's responsibility. A consumer who wants an ellipse-shaped node must both set `Node.Shape = new EllipseShape()` (for port placement) and provide a DataTemplate that renders an ellipse (for visuals).

**Known limitation — rectangular interaction:** Node hit-testing, marquee selection, hover borders, and drag targeting remain rectangle-based (`Node.Width` x `Node.Height`), even when `Node.Shape` describes a non-rectangular boundary. This means an ellipse node is still selectable and hover-highlighted in its transparent corner area. Extending `INodeShape` to influence hit-testing and overlay rendering is a future enhancement, not part of this spec.

### Built-in shapes

- `RectangleShape` — ray-rectangle intersection (default)
- `EllipseShape` — ray-ellipse intersection
- `RoundedRectangleShape(double cornerRadius)` — rectangle with rounded corners

### Node model change

- `Node.Shape` : `INodeShape` (default `RectangleShape`)

### ILayoutAwarePortProvider interface

New interface extending `IPortProvider` for providers that need to react to node size/shape changes:

```csharp
public interface ILayoutAwarePortProvider : IPortProvider
{
    void UpdateLayout(double width, double height, INodeShape? shape);
}
```

The canvas checks `node.PortProvider is ILayoutAwarePortProvider` after measure/arrange and calls `UpdateLayout` with current dimensions and the node's shape. Providers that don't implement this interface are unaffected — their ports stay at fixed positions.

### AnglePortProvider

New port provider alongside existing ones. Implements both `IPortProvider` and `ILayoutAwarePortProvider`. No deprecation of `FixedPortProvider` or `DynamicPortProvider`.

```
AnglePortProvider : ILayoutAwarePortProvider
  Ports : IReadOnlyList<Port>
  ResolvePort(Point) : Port?
  UpdateLayout(double width, double height, INodeShape? shape)
  DistributeEvenly() — spaces all ports at equal angular intervals
```

`UpdateLayout` is called by the canvas after node measure. Computes `Port.Position` from each port's `Angle` + the node's shape + current dimensions.

### Canvas invalidation contract

The canvas is responsible for triggering redraws after port positions change. The contract is:

1. Canvas calls `ILayoutAwarePortProvider.UpdateLayout()` during `ArrangeOverride`
2. Canvas calls `InvalidateVisual()` immediately after, which causes `CanvasOverlay` to re-render ports, labels, and connections at updated positions
3. Hit-test queries (`HitTestPort`, connection endpoints) always read `Port.AbsolutePosition` live — no cached positions

Port INPC (`PropertyChanged` on `Position`, `Angle`, etc.) is for **consumer code** that wants to react to port changes (e.g., updating a sidebar inspector). The canvas rendering path does not subscribe to individual port PropertyChanged events — it invalidates via the UpdateLayout call path above.

### Runtime port-geometry changes

`Port.Angle` is publicly settable. Changing it at runtime (outside ArrangeOverride) must also recompute the port's position and trigger a redraw. The contract:

1. `AnglePortProvider` subscribes to each managed port's `PropertyChanged`
2. When `Angle` changes, the provider immediately recomputes that port's `Position` (using the last-known width, height, and shape from the most recent `UpdateLayout` call)
3. `AnglePortProvider` raises a `LayoutInvalidated` event
4. The canvas subscribes to `ILayoutAwarePortProvider.LayoutInvalidated` and calls `InvalidateVisual()` when fired

This extends the `ILayoutAwarePortProvider` interface:

```csharp
public interface ILayoutAwarePortProvider : IPortProvider
{
    void UpdateLayout(double width, double height, INodeShape? shape);
    event Action? LayoutInvalidated;
}
```

This keeps the provider and canvas decoupled — the provider doesn't reference the canvas, it just raises an event. The canvas subscribes when a node's port provider is `ILayoutAwarePortProvider` and unsubscribes when the node is removed.

### Label auto-positioning

When `LabelPlacement` is null (default), derived from angle:

| Angle range   | Default label side |
|---------------|--------------------|
| 315-45 (top)  | Below              |
| 45-135 (right)| Left               |
| 135-225 (bottom)| Above            |
| 225-315 (left)| Right              |

Labels rendered by CanvasOverlay adjacent to port shapes, small text (10px default), foreground from theme resource. Labels follow the same rendering gate as default port visuals: when `PortTemplate` is set on the canvas, both default port shapes and labels are suppressed.

**Note on PortTemplate:** Today `PortTemplate` is only a suppression flag — setting it disables overlay-rendered port visuals, but there is no mechanism that actually instantiates or arranges per-port controls from the template. Building a real port-hosting model (templated visual children per port) is out of scope for this spec. Consumers who set `PortTemplate` today get no port visuals; this spec does not change that behavior. A proper port-hosting system is a future enhancement.

### Backward compatibility

`FixedPortProvider` and `DynamicPortProvider` remain unchanged as equal options. Different use cases, different providers. Consumers can use any provider they choose.

## 4. Node Collapse

Per-node toggle that collapses the node, hiding body content and ports. Collapse has two distinct layers: behavioral (canvas-enforced) and visual (template-driven).

### Model change

- `Node.IsCollapsed` : `bool` (default `false`, INPC)

### Behavioral collapse (canvas-enforced, always applies)

These behaviors are enforced by the canvas regardless of which template the node uses:

- **Ports hidden:** not rendered by CanvasOverlay, not hit-testable when `IsCollapsed == true`
- **Connection blocking:** new connections cannot be started from or dropped onto a collapsed node's ports
- **Existing connections remain:** connection endpoints use the port's current `AbsolutePosition` (which may or may not have changed depending on whether the template shrinks the node)
- **Port repositioning via UpdateLayout:** port providers that implement `ILayoutAwarePortProvider` receive the node's current dimensions (whatever the template rendered) via `UpdateLayout` and reposition ports accordingly:
  - **AnglePortProvider**: recalculates boundary points against current node size
  - **FixedPortProvider**: does not implement `ILayoutAwarePortProvider` — ports retain original positions
  - **DynamicPortProvider**: does not implement `ILayoutAwarePortProvider` — ports retain original positions

### Visual collapse (template-driven)

How the node visually shrinks is the template's responsibility:

- **Default Node template**: collapses to header-only (body hidden). When `ShowHeader == false`, shows minimal pill/bar indicator. `Node.Height` updates to reflect collapsed size after layout.
- **CommentNodeTemplate / GroupNodeTemplate**: these built-in templates do not handle `IsCollapsed` in this spec. They render at full size. Behavioral collapse still applies (ports hidden, connections blocked). Adding visual collapse to specialized templates is a future enhancement.
- **Custom templates**: should bind to `Node.IsCollapsed` for visual consistency. If they don't, the node remains visually full-size but behavioral collapse still applies — ports are hidden and connections are blocked, which may look inconsistent to the user.

| ShowHeader | IsCollapsed | Default template renders                        |
|------------|-------------|--------------------------------------------------|
| true       | false       | Full node (header + body)                        |
| true       | true        | Header bar only, body hidden                     |
| false      | false       | Body only (no header)                            |
| false      | true        | Minimal pill/bar indicator with node border style |

### Collapse trigger

No built-in collapse button. Consumer sets `node.IsCollapsed = true` via their own UI (double-click handler, context menu, toolbar, etc.).

### Sizing

- Collapsed height depends on the template (default template: header height or minimal indicator)
- Width unchanged
- `Node.Height` updates to reflect whatever the template actually renders after layout

## 5. Grid Snap Ghost

Enhances existing `SnapToGrid` with visual feedback during drag.

### Canvas property

- `NodiumGraphCanvas.ShowSnapGhost` : `bool` (default `false`)

### Behavior

- Only active when `SnapToGrid == true` AND `ShowSnapGhost == true`
- While dragging a node:
  - Node stays under the cursor at exact drag position (smooth movement)
  - Ghost outline appears at nearest grid-snapped position
  - Ghost is translucent (~30% opacity) version of node border (uses NodeStyle.BorderBrush or theme default)
  - Ghost hidden when snapped position equals unsnapped position (node already at grid point — no visual noise)
- On release: node lands at ghost position (snapped to grid)
- When `ShowSnapGhost == false` and `SnapToGrid == true`: existing behavior (node jumps between grid points)

### Rendering

- Ghost rendered by CanvasOverlay as rectangle outline matching node dimensions
- Same rendering pass as selection/hover borders

## Implementation Order

1. **NodeStyle & PortStyle** — foundation, everything else depends on it
2. **Header Toggle** | **Angle-based Ports + Labels + Shapes** | **Grid Snap Ghost** — parallel, independent of each other, all depend on phase 1
3. **Node Collapse** — depends on header toggle + port system being in place

## Implementation Tasks

- Task #6: NodeStyle & PortStyle
- Task #7: Header Toggle (blocked by #6)
- Task #8: Angle-based Ports, Labels, Shapes (blocked by #6)
- Task #9: Node Collapse (blocked by #7, #8)
- Task #10: Grid Snap Ghost (blocked by #6)
