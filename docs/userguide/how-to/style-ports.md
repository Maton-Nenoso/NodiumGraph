# Style Ports

## Goal

Customise how ports look — their size, shape, fill, outline, label typography — either globally across every node or on a single port individually. Also: know where the "hover" / "valid" / "invalid" feedback during a connection drag comes from, so you can theme it too.

## Prerequisites

- You already host `NodiumGraphCanvas`. See [Host the Canvas](host-canvas.md).
- You have ports on your nodes (`FixedPortProvider`, `DynamicPortProvider`, or a [custom provider](custom-port-provider.md)).

## Steps

### 1. Understand the three layers

Port visuals resolve through three layers, in order of precedence:

1. **Per-port `Port.Style`** — an optional `PortStyle` instance on each `Port`. Any non-null property wins over everything else.
2. **Theme resources** — the brush / size keys on `NodiumGraph.NodiumGraphResources` (`PortBrushKey`, `PortOutlineBrushKey`, `PortLabelBrushKey`, `PortLabelFontSizeKey`, `PortLabelOffsetKey`). Set them in your application or window resource dictionary. See [Theme the canvas](theme-canvas.md).
3. **Library defaults** — hardcoded fallbacks used when neither of the above provides a value.

Use theme resources for "every port in my app looks like this". Use `Port.Style` for "this specific port is different because it represents something special".

### 2. Change all ports at once with theme resources

Drop these into `Application.Resources` or a `Window.Resources` block:

```xml
<Application.Resources>
  <ResourceDictionary>
    <SolidColorBrush x:Key="NodiumGraphPortBrush" Color="#0EA5E9" />
    <SolidColorBrush x:Key="NodiumGraphPortOutlineBrush" Color="#0369A1" />
    <SolidColorBrush x:Key="NodiumGraphPortLabelBrush" Color="#334155" />
    <x:Double x:Key="NodiumGraphPortLabelFontSize">10</x:Double>
    <x:Double x:Key="NodiumGraphPortLabelOffset">10</x:Double>
  </ResourceDictionary>
</Application.Resources>
```

Every `NodiumGraphCanvas` in the app now renders ports with that fill, outline, label typography, and label gap. No per-port overrides required.

### 3. Override one port with `Port.Style`

```csharp
using Avalonia.Media;
using NodiumGraph.Model;

var trigger = new Port(node, "trigger", PortFlow.Input, new Point(0, 40))
{
    Label = "trigger",
    Style = new PortStyle
    {
        Fill = new SolidColorBrush(Color.Parse("#F59E0B")),
        Stroke = new SolidColorBrush(Color.Parse("#B45309")),
        StrokeWidth = 2.0,
        Shape = PortShape.Diamond,
        Size = 7,
        LabelFontSize = 10,
        LabelBrush = new SolidColorBrush(Color.Parse("#78350F")),
        LabelOffset = 10,
        LabelPlacement = PortLabelPlacement.Right,
    },
};
```

Every property on `PortStyle` is nullable. Setting `Shape = PortShape.Diamond` and leaving everything else null gives you the default color and size, but a diamond shape. Clearing the whole style (`port.Style = null`) reverts the port to theme + defaults.

### 4. Available `PortStyle` properties

| Property | Type | Effect |
|---|---|---|
| `Fill` | `IBrush?` | Port body fill |
| `Stroke` | `IBrush?` | Port outline brush |
| `StrokeWidth` | `double?` | Outline thickness in pixels |
| `Shape` | `PortShape?` | `Circle`, `Square`, `Diamond`, or `Triangle` |
| `Size` | `double?` | Radius (circle) or half-side (square / diamond / triangle) |
| `LabelFontSize` | `double?` | Label text size (default 11) |
| `LabelBrush` | `IBrush?` | Label text brush |
| `LabelOffset` | `double?` | Gap between the port and its label (default 8) |
| `LabelPlacement` | `PortLabelPlacement?` | `Left`, `Right`, `Above`, `Below`. When `null`, placement is automatic based on which side of the node the port sits on. |

`PortStyle` implements `INotifyPropertyChanged`, so mutating a field on an already-assigned style triggers a redraw without reassigning `Port.Style`.

### 5. Hover, valid, and invalid feedback

During a connection drag, the canvas shows live feedback on candidate target ports — "this one is valid, that one isn't". These colors are **not** set on `PortStyle`; they come from the theme keys:

| Feedback | Resource key | Default |
|---|---|---|
| Valid target | `NodiumGraphConnectionPreviewValidBrush` | theme-provided green |
| Invalid target | `NodiumGraphConnectionPreviewInvalidBrush` | theme-provided red |
| Hovered node border | `NodiumGraphNodeHoveredBorderBrush` | theme-provided blue |

Override them in your resource dictionary the same way as any other theme key:

```xml
<SolidColorBrush x:Key="NodiumGraphConnectionPreviewValidBrush" Color="#10B981" />
<SolidColorBrush x:Key="NodiumGraphConnectionPreviewInvalidBrush" Color="#DC2626" />
<SolidColorBrush x:Key="NodiumGraphNodeHoveredBorderBrush" Color="#3B82F6" />
```

There is currently no per-port override for hover / valid / invalid — the feedback is uniform across the canvas so users always see the same signal. If you need per-port variation, it's not in the library today; file an issue or fork.

### 6. Shape / size per `PortFlow`

If you want "inputs are squares, outputs are circles", do it at construction time by assigning different `PortStyle` instances. There is no automatic binding of shape to `PortFlow`, but it's a one-liner to write:

```csharp
static PortStyle StyleFor(PortFlow flow) => flow switch
{
    PortFlow.Input => new PortStyle { Shape = PortShape.Square, Size = 6 },
    PortFlow.Output => new PortStyle { Shape = PortShape.Circle, Size = 6 },
    _ => new PortStyle(),
};

var inPort = new Port(node, "in", PortFlow.Input, new Point(0, 40))
{
    Label = "in",
    Style = StyleFor(PortFlow.Input),
};
```

## Full code

```csharp
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Model;

static Port MakeTypedPort(Node owner, string id, string label, PortFlow flow, Point position, string kind)
{
    var shape = flow == PortFlow.Input ? PortShape.Square : PortShape.Circle;
    var color = kind switch
    {
        "number" => "#0EA5E9",
        "text"   => "#10B981",
        "trigger" => "#F59E0B",
        _ => "#64748B",
    };

    return new Port(owner, id, flow, position)
    {
        Label = label,
        DataType = kind,
        Style = new PortStyle
        {
            Fill = new SolidColorBrush(Color.Parse(color)),
            Shape = shape,
            Size = 6,
        },
    };
}
```

```xml
<!-- App.axaml -->
<Application.Resources>
  <SolidColorBrush x:Key="NodiumGraphPortBrush" Color="#64748B" />
  <SolidColorBrush x:Key="NodiumGraphPortOutlineBrush" Color="#334155" />
  <SolidColorBrush x:Key="NodiumGraphPortLabelBrush" Color="#475569" />
  <SolidColorBrush x:Key="NodiumGraphConnectionPreviewValidBrush" Color="#10B981" />
  <SolidColorBrush x:Key="NodiumGraphConnectionPreviewInvalidBrush" Color="#DC2626" />
</Application.Resources>
```

## Gotchas

- **`Port.Style` always wins over theme resources.** If a global theme change isn't showing up on a particular port, check whether that port has its own `Style` set.
- **`Size` is a radius for circles, a half-side for other shapes.** A circle with `Size = 6` and a square with `Size = 6` both fit inside a 12-pixel box, which is usually what you want when mixing shapes.
- **`PortStyle` properties are nullable — `null` means "fall through".** Setting `Fill = Brushes.Transparent` is not the same as unsetting it; use `null` when you want the theme or default to take effect.
- **`LabelPlacement = null` uses a position heuristic** based on which side of the node the port sits on (left-side ports label to the left, right-side ports label to the right). Only override it when the heuristic is wrong for your layout.
- **Hover / valid / invalid are canvas-wide.** There's no per-port override for those states today. If your domain needs different warning colors for different port kinds, you'd need to layer your own visuals on top, not tweak `PortStyle`.
- **`PortStyle` mutations raise `PropertyChanged` on the style instance** — the canvas watches for this and invalidates the port's visual. You do not need to reassign `Port.Style` after mutating it; just change the property and the next frame picks it up.
- **`SolidColorBrush` allocations matter at scale.** If every port constructs its own brush, you end up with thousands of identical brush instances on a large graph. Hoist common brushes to static fields or shared dictionaries.

## See also

- [Theme the canvas](theme-canvas.md)
- [Custom port provider](custom-port-provider.md)
- [Strategy interfaces reference](../reference/strategies.md#iportprovider)
- [Model reference](../reference/model.md)
- [Define a custom node DataTemplate](custom-node-template.md)
