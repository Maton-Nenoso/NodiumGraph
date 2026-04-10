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

### Custom templates

Consumers using custom templates can ignore `ShowHeader`. It is only respected by the built-in default template. Custom templates can bind to it if desired.

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

### Label auto-positioning

When `LabelPlacement` is null (default), derived from angle:

| Angle range   | Default label side |
|---------------|--------------------|
| 315-45 (top)  | Below              |
| 45-135 (right)| Left               |
| 135-225 (bottom)| Above            |
| 225-315 (left)| Right              |

Labels rendered by CanvasOverlay adjacent to port shapes, small text (10px default), foreground from theme resource. When a custom `PortTemplate` is set on the canvas, label rendering is skipped (consistent with how default port visuals are skipped — the custom template takes full responsibility).

### Backward compatibility

`FixedPortProvider` and `DynamicPortProvider` remain unchanged as equal options. Different use cases, different providers. Consumers can use any provider they choose.

## 4. Node Collapse

Per-node toggle that collapses the node to header-only, hiding body content and ports.

### Model change

- `Node.IsCollapsed` : `bool` (default `false`, INPC)

### Visual behavior

See Header Toggle section for the ShowHeader x IsCollapsed interaction table.

### Port behavior when collapsed

- Ports are hidden visually (not rendered by CanvasOverlay, not hit-testable)
- Ports still exist on the model
- Port providers that implement `ILayoutAwarePortProvider` receive collapsed dimensions via `UpdateLayout` and reposition ports to the collapsed boundary:
  - **AnglePortProvider**: recalculates boundary points against collapsed node size
  - **FixedPortProvider**: does not implement `ILayoutAwarePortProvider` — ports retain their original positions. Connections to ports outside collapsed bounds will visually extend beyond the collapsed node. This is acceptable since the ports still exist logically.
  - **DynamicPortProvider**: does not implement `ILayoutAwarePortProvider` — same behavior as FixedPortProvider.
- For providers implementing `ILayoutAwarePortProvider`, existing connections follow ports to collapsed positions (connections visually "pull in" toward smaller node)
- New connections cannot be started from or dropped onto collapsed node ports

### Collapse trigger

No built-in collapse button. Consumer sets `node.IsCollapsed = true` via their own UI (double-click handler, context menu, toolbar, etc.).

### Sizing

- Collapsed height = header height only (or minimal indicator height if headerless)
- Width unchanged
- `Node.Height` updates to reflect collapsed size after layout

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
