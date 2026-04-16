# Style Connections with Arrowheads

## Goal

Add endpoint decorations — arrowheads, diamonds, circles, or bars — to connections so that direction and relationship type are visually clear.

## Prerequisites

- You already host `NodiumGraphCanvas` and have connections rendering. See [Host the Canvas](host-canvas.md).
- You know how `IConnectionStyle` works. See [Custom connection style](custom-style.md).

## Steps

### 1. Choose an endpoint renderer

NodiumGraph ships five built-in `IEndpointRenderer` implementations:

| Renderer | Shape | `IsFilled` | Typical use |
|---|---|---|---|
| `NoneEndpoint.Instance` | nothing | false | No decoration (default when `null`) |
| `ArrowEndpoint(size, filled)` | triangle | configurable | Directed flow, data direction |
| `DiamondEndpoint(size, filled)` | rhombus | configurable | UML composition (filled) / aggregation (open) |
| `CircleEndpoint(radius, filled)` | disc | configurable | Relationship marker |
| `BarEndpoint(width)` | perpendicular bar | always false | Cardinality indicator |

Each endpoint is stateless and immutable — construct once, share across styles.

### 2. Assign endpoints via `ConnectionStyle`

`IConnectionStyle` has two endpoint properties: `SourceEndpoint` (start of the connection) and `TargetEndpoint` (end). Both default to `null`, meaning no decoration.

The simplest case — a filled arrow at the target end:

```csharp
using NodiumGraph.Interactions;

Canvas.DefaultConnectionStyle = new ConnectionStyle(
    targetEndpoint: new ArrowEndpoint());
```

Both ends can be decorated independently:

```csharp
Canvas.DefaultConnectionStyle = new ConnectionStyle(
    sourceEndpoint: new CircleEndpoint(radius: 4, filled: true),
    targetEndpoint: new ArrowEndpoint(size: 10, filled: true));
```

### 3. Use different styles for different connection kinds

If some connections represent data flow and others represent inheritance, create separate styles and resolve per-connection. Since `IConnectionStyle` is canvas-wide by default, you implement per-connection variation through the style provider mechanism:

```csharp
// Define styles once — share instances across the app.
private static readonly ConnectionStyle FlowStyle = new(
    stroke: new SolidColorBrush(Color.Parse("#475569")),
    thickness: 2.0,
    targetEndpoint: new ArrowEndpoint(size: 8, filled: true));

private static readonly ConnectionStyle InheritanceStyle = new(
    stroke: new SolidColorBrush(Color.Parse("#7C3AED")),
    thickness: 2.0,
    targetEndpoint: new DiamondEndpoint(size: 8, filled: true));
```

### 4. Open vs filled variants

Every endpoint renderer (except `BarEndpoint`, which is always open) accepts a `filled` parameter:

- **Filled** (`filled: true`): the shape is closed and painted with the connection's stroke brush. Use for strong relationships — UML composition, mandatory data flow.
- **Open** (`filled: false`): the shape is closed but only stroked, not filled. Use for weaker or optional relationships — UML aggregation, optional flow.

```csharp
// Filled arrow (solid triangle)
new ArrowEndpoint(size: 8, filled: true)

// Open arrow (chevron outline)
new ArrowEndpoint(size: 8, filled: false)

// Filled diamond (UML composition)
new DiamondEndpoint(size: 8, filled: true)

// Open diamond (UML aggregation)
new DiamondEndpoint(size: 8, filled: false)
```

### 5. Combine with other style properties

Endpoint decorations compose with stroke, thickness, and dash pattern:

```csharp
Canvas.DefaultConnectionStyle = new ConnectionStyle(
    stroke: new SolidColorBrush(Color.Parse("#64748B")),
    thickness: 1.5,
    dashPattern: new DashStyle(new double[] { 4, 2 }, 0),
    targetEndpoint: new ArrowEndpoint(size: 8, filled: false));
```

The endpoint geometry scales with `strokeThickness` where appropriate — the renderer receives the current thickness and can use it to size details like the bar height in `BarEndpoint`.

## Full code

```csharp
using Avalonia.Media;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;

// In your MainWindow or setup code:

// Style A: directed data flow — filled arrow at target
var flowStyle = new ConnectionStyle(
    stroke: new SolidColorBrush(Color.Parse("#475569")),
    thickness: 2.0,
    targetEndpoint: new ArrowEndpoint(size: 8, filled: true));

// Style B: inheritance — filled diamond at target, open circle at source
var inheritanceStyle = new ConnectionStyle(
    stroke: new SolidColorBrush(Color.Parse("#7C3AED")),
    thickness: 2.0,
    sourceEndpoint: new CircleEndpoint(radius: 4, filled: false),
    targetEndpoint: new DiamondEndpoint(size: 8, filled: true));

// Apply the default style to the canvas
Canvas.DefaultConnectionStyle = flowStyle;
```

## Gotchas

- **Endpoints are `null` by default, not `NoneEndpoint`.** Both mean "no decoration", but `null` skips the endpoint rendering path entirely — zero overhead. Use `NoneEndpoint.Instance` only when you need an explicit "none" value in data-driven code.
- **Inset shortens the stroke automatically.** When an endpoint renderer is attached, the connection line is shortened by `GetInset(thickness)` world units so the stroke terminates at the base of the shape, not inside it. You don't need to adjust anything — this is handled by the renderer.
- **Construct endpoint instances once.** `ArrowEndpoint`, `DiamondEndpoint`, etc. are immutable value-like objects. Create a `static readonly` instance and reuse it across styles. Don't construct a new instance per frame or per connection.
- **The `size` / `radius` parameter is in world units.** At zoom 1.0, `size: 8` means 8 pixels. At zoom 2.0, it renders as 16 pixels on screen. This matches how connection thickness behaves — everything scales uniformly with the viewport.
- **`BarEndpoint` is never filled.** Its `IsFilled` always returns `false`. The bar is always rendered as a stroked line perpendicular to the connection direction.

## See also

- [IEndpointRenderer reference](../3-reference/connection-api.md)
- [Custom connection style](custom-style.md)
- [Rendering pipeline reference](../3-reference/rendering-pipeline.md)
- [Custom router](custom-router.md)
