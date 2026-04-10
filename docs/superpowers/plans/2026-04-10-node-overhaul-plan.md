# Node Overhaul Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers-extended-cc:subagent-driven-development (if subagents available) or superpowers-extended-cc:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add style objects, header toggle, angle-based port positioning, node collapse, and grid snap ghost to NodiumGraph.

**Architecture:** Style-first approach — build NodeStyle/PortStyle foundation, then layer header toggle, angle-based ports, and snap ghost in parallel, then node collapse on top. TDD throughout. All new model classes use the existing `SetField<T>` INPC pattern from `Node.cs:71-78`.

**Tech Stack:** C# / .NET 10, Avalonia 12, xUnit v3 + Avalonia headless testing

**Spec:** `docs/superpowers/specs/2026-04-10-node-overhaul-design.md`

---

## File Structure

### New Files

| Path | Responsibility |
|------|---------------|
| `src/NodiumGraph/Model/NodeStyle.cs` | Visual override object for nodes (brushes, border, opacity) |
| `src/NodiumGraph/Model/PortStyle.cs` | Visual override object for ports (fill, stroke, shape, size) |
| `src/NodiumGraph/Model/PortShape.cs` | Enum: Circle, Square, Diamond, Triangle |
| `src/NodiumGraph/Model/PortLabelPlacement.cs` | Enum: Left, Right, Above, Below |
| `src/NodiumGraph/Model/INodeShape.cs` | Interface for boundary point resolution |
| `src/NodiumGraph/Model/RectangleShape.cs` | Ray-rectangle intersection |
| `src/NodiumGraph/Model/EllipseShape.cs` | Ray-ellipse intersection |
| `src/NodiumGraph/Model/RoundedRectangleShape.cs` | Ray-rounded-rect intersection |
| `src/NodiumGraph/Model/ILayoutAwarePortProvider.cs` | Interface extending IPortProvider with UpdateLayout + LayoutInvalidated |
| `src/NodiumGraph/Model/AnglePortProvider.cs` | Angle-based port positioning with shape boundary resolution |
| `tests/NodiumGraph.Tests/NodeStyleTests.cs` | NodeStyle property tests |
| `tests/NodiumGraph.Tests/PortStyleTests.cs` | PortStyle property tests |
| `tests/NodiumGraph.Tests/AnglePortProviderTests.cs` | Angle port positioning tests |
| `tests/NodiumGraph.Tests/NodeShapeTests.cs` | Shape boundary math tests |

### Modified Files

| Path | Changes |
|------|---------|
| `src/NodiumGraph/Model/Node.cs` | Add `Style`, `ShowHeader`, `IsCollapsed`, `Shape` properties |
| `src/NodiumGraph/Model/Port.cs` | Add INPC, `Style`, `Angle`, `Label`, `LabelPlacement` properties; `Position` becomes `{ get; internal set; }` |
| `src/NodiumGraph/Controls/DefaultTemplates.cs` | Respect NodeStyle, ShowHeader, IsCollapsed in default template |
| `src/NodiumGraph/Controls/CanvasOverlay.cs` | Render PortStyle shapes/labels, hide ports when collapsed, draw snap ghost |
| `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` | Add `ShowSnapGhost` property; call UpdateLayout; subscribe to LayoutInvalidated; block connections on collapsed nodes; smooth drag with ghost |
| `src/NodiumGraph/NodiumGraphResources.cs` | Add port label resource key |

---

## Task 1: NodeStyle & PortStyle (Task #6)

**Files:**
- Create: `src/NodiumGraph/Model/PortShape.cs`
- Create: `src/NodiumGraph/Model/NodeStyle.cs`
- Create: `src/NodiumGraph/Model/PortStyle.cs`
- Create: `tests/NodiumGraph.Tests/NodeStyleTests.cs`
- Create: `tests/NodiumGraph.Tests/PortStyleTests.cs`
- Modify: `src/NodiumGraph/Model/Node.cs:50` (add Style property)
- Modify: `src/NodiumGraph/Model/Port.cs` (add INPC infrastructure + Style property)
- Modify: `src/NodiumGraph/Controls/DefaultTemplates.cs:17-56` (respect NodeStyle in default template)
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs:56-78` (respect PortStyle in port rendering)

### Step 1.1: Create PortShape enum

- [ ] Create `src/NodiumGraph/Model/PortShape.cs`:

```csharp
namespace NodiumGraph.Model;

public enum PortShape
{
    Circle,
    Square,
    Diamond,
    Triangle
}
```

### Step 1.2: Write NodeStyle tests

- [ ] Create `tests/NodiumGraph.Tests/NodeStyleTests.cs` with tests for:
  - All properties default to null
  - Setting each property raises PropertyChanged
  - Setting same value does not raise PropertyChanged

### Step 1.3: Implement NodeStyle

- [ ] Create `src/NodiumGraph/Model/NodeStyle.cs`:

```csharp
namespace NodiumGraph.Model;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

public class NodeStyle : INotifyPropertyChanged
{
    private IBrush? _headerBackground;
    private IBrush? _headerForeground;
    private IBrush? _bodyBackground;
    private IBrush? _borderBrush;
    private double? _borderThickness;
    private CornerRadius? _cornerRadius;
    private double? _opacity;

    public IBrush? HeaderBackground { get => _headerBackground; set => SetField(ref _headerBackground, value); }
    public IBrush? HeaderForeground { get => _headerForeground; set => SetField(ref _headerForeground, value); }
    public IBrush? BodyBackground { get => _bodyBackground; set => SetField(ref _bodyBackground, value); }
    public IBrush? BorderBrush { get => _borderBrush; set => SetField(ref _borderBrush, value); }
    public double? BorderThickness { get => _borderThickness; set => SetField(ref _borderThickness, value); }
    public CornerRadius? CornerRadius { get => _cornerRadius; set => SetField(ref _cornerRadius, value); }
    public double? Opacity { get => _opacity; set => SetField(ref _opacity, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] Run `dotnet test --filter "FullyQualifiedName~NodeStyleTests"` — verify all pass.

### Step 1.4: Write PortStyle tests

- [ ] Create `tests/NodiumGraph.Tests/PortStyleTests.cs` with tests for:
  - All properties default to null
  - Setting each property raises PropertyChanged

### Step 1.5: Implement PortStyle

- [ ] Create `src/NodiumGraph/Model/PortStyle.cs`:

```csharp
namespace NodiumGraph.Model;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

public class PortStyle : INotifyPropertyChanged
{
    private IBrush? _fill;
    private IBrush? _stroke;
    private double? _strokeWidth;
    private PortShape? _shape;
    private double? _size;

    public IBrush? Fill { get => _fill; set => SetField(ref _fill, value); }
    public IBrush? Stroke { get => _stroke; set => SetField(ref _stroke, value); }
    public double? StrokeWidth { get => _strokeWidth; set => SetField(ref _strokeWidth, value); }
    public PortShape? Shape { get => _shape; set => SetField(ref _shape, value); }
    public double? Size { get => _size; set => SetField(ref _size, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] Run `dotnet test --filter "FullyQualifiedName~PortStyleTests"` — verify all pass.

### Step 1.6: Add Style property to Node and Port

- [ ] Add to `Node.cs` after the `PortProvider` property (line 50):

```csharp
private NodeStyle? _style;
public NodeStyle? Style { get => _style; set => SetField(ref _style, value); }
```

- [ ] Add INPC infrastructure to `Port.cs` — same `SetField<T>` pattern as Node. Add `Style` property:

```csharp
private PortStyle? _style;
public PortStyle? Style { get => _style; set => SetField(ref _style, value); }
```

Change `Position` from `{ get; init; }` to `{ get; internal set; }` with INPC notification. Keep existing constructors working.

- [ ] Add tests to `NodeTests.cs`: Node.Style defaults to null, raises PropertyChanged.
- [ ] Add tests to `PortTests.cs`: Port.Style defaults to null, raises PropertyChanged. Port.Position raises PropertyChanged when set internally.
- [ ] Run `dotnet test` — verify all pass (including existing tests).

### Step 1.7: Respect NodeStyle in DefaultTemplates

- [ ] Modify `DefaultTemplates.cs:17-56` — the NodeTemplate FuncDataTemplate body. When building the header Border and body Border, check `node.Style` properties and fall back to theme resources:

Resolution pattern (use throughout):
```csharp
// Example for header background
var headerBg = node.Style?.HeaderBackground
    ?? canvas.ResolveBrush(NodiumGraphResources.NodeHeaderBrushKey, defaultBrush);
```

Apply this for: HeaderBackground, HeaderForeground, BodyBackground, BorderBrush, BorderThickness, CornerRadius, Opacity.

**Note:** The default template is a `FuncDataTemplate` that creates controls once. For runtime style changes to take effect, the canvas must rebuild the template when `Node.Style` or its properties change. The simplest approach: subscribe to `Node.PropertyChanged` for "Style" and `NodeStyle.PropertyChanged`, and rebuild the node container's `ContentTemplate` when either fires. Add this subscription in `AddNodeContainer` and cleanup in `RemoveNodeContainer`.

- [ ] Run `dotnet test` — verify all pass.

### Step 1.8: Respect PortStyle in CanvasOverlay

- [ ] Modify `CanvasOverlay.cs:56-78` — the port rendering section. For each port, check `port.Style` before using defaults:

```csharp
var fill = port.Style?.Fill ?? canvas.ResolveBrush(NodiumGraphResources.PortBrushKey, defaultPortBrush);
var stroke = port.Style?.Stroke ?? canvas.ResolveBrush(NodiumGraphResources.PortOutlineBrushKey, defaultOutlineBrush);
var strokeWidth = port.Style?.StrokeWidth ?? 1.0;
var size = port.Style?.Size ?? 8.0;
var shape = port.Style?.Shape ?? PortShape.Circle;
```

Draw the appropriate shape based on `shape`:
- Circle: existing ellipse drawing with `size` as diameter
- Square: `DrawRectangle` centered on port position
- Diamond: rotate a square 45 degrees via `DrawingContext.PushTransform`
- Triangle: `StreamGeometry` with 3 points

- [ ] Run `dotnet test` — verify all pass.

### Step 1.9: Commit

- [ ] `git add -A && git commit -m "feat: add NodeStyle and PortStyle with per-instance visual overrides"`

---

## Task 2: Header Toggle (Task #7)

**Files:**
- Modify: `src/NodiumGraph/Model/Node.cs` (add ShowHeader)
- Modify: `src/NodiumGraph/Controls/DefaultTemplates.cs` (conditional header)
- Test: `tests/NodiumGraph.Tests/NodeTests.cs`

### Step 2.1: Write failing tests

- [ ] Add to `NodeTests.cs`:
  - `ShowHeader_defaults_to_true`
  - `ShowHeader_raises_PropertyChanged`

### Step 2.2: Add ShowHeader to Node

- [ ] Add to `Node.cs`:

```csharp
private bool _showHeader = true;
public bool ShowHeader { get => _showHeader; set => SetField(ref _showHeader, value); }
```

- [ ] Run `dotnet test --filter "FullyQualifiedName~NodeTests"` — verify pass.

### Step 2.3: Update DefaultTemplates to respect ShowHeader

- [ ] Modify the default NodeTemplate in `DefaultTemplates.cs`. The header Border's `IsVisible` should be bound to `node.ShowHeader`. Since FuncDataTemplate builds controls, set `IsVisible` directly:

```csharp
headerBorder.IsVisible = node.ShowHeader;
```

The canvas must listen for `ShowHeader` changes on nodes and trigger a re-measure (since the header collapsing changes node height). Add "ShowHeader" to the list of node property changes that trigger `InvalidateMeasure()` in the canvas's node PropertyChanged handler.

- [ ] Run `dotnet test` — verify all pass.

### Step 2.4: Commit

- [ ] `git add -A && git commit -m "feat: add ShowHeader toggle to hide node title bar"`

---

## Task 3: Angle-Based Ports, Labels, and Node Shapes (Task #8)

**Files:**
- Create: `src/NodiumGraph/Model/PortLabelPlacement.cs`
- Create: `src/NodiumGraph/Model/INodeShape.cs`
- Create: `src/NodiumGraph/Model/RectangleShape.cs`
- Create: `src/NodiumGraph/Model/EllipseShape.cs`
- Create: `src/NodiumGraph/Model/RoundedRectangleShape.cs`
- Create: `src/NodiumGraph/Model/ILayoutAwarePortProvider.cs`
- Create: `src/NodiumGraph/Model/AnglePortProvider.cs`
- Create: `tests/NodiumGraph.Tests/NodeShapeTests.cs`
- Create: `tests/NodiumGraph.Tests/AnglePortProviderTests.cs`
- Modify: `src/NodiumGraph/Model/Port.cs` (add Angle, Label, LabelPlacement)
- Modify: `src/NodiumGraph/Model/Node.cs` (add Shape)
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:1199-1222` (call UpdateLayout in ArrangeOverride, subscribe LayoutInvalidated)
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs:56-78` (render labels)
- Modify: `src/NodiumGraph/NodiumGraphResources.cs` (add PortLabelBrushKey)

### Step 3.1: Create enums and interfaces

- [ ] Create `src/NodiumGraph/Model/PortLabelPlacement.cs`:

```csharp
namespace NodiumGraph.Model;

public enum PortLabelPlacement
{
    Left,
    Right,
    Above,
    Below
}
```

- [ ] Create `src/NodiumGraph/Model/INodeShape.cs`:

```csharp
namespace NodiumGraph.Model;

using Avalonia;

public interface INodeShape
{
    /// <summary>
    /// Returns the boundary point for a ray from node center at the given angle.
    /// Point is relative to node center. 0 degrees = top, clockwise.
    /// </summary>
    Point GetBoundaryPoint(double angleDegrees, double width, double height);
}
```

- [ ] Create `src/NodiumGraph/Model/ILayoutAwarePortProvider.cs`:

```csharp
namespace NodiumGraph.Model;

public interface ILayoutAwarePortProvider : IPortProvider
{
    void UpdateLayout(double width, double height, INodeShape? shape);
    event Action? LayoutInvalidated;
}
```

### Step 3.2: Write shape boundary tests

- [ ] Create `tests/NodiumGraph.Tests/NodeShapeTests.cs` with tests:

**RectangleShape:**
- `Angle_0_returns_top_center` — (0, -h/2) for angle 0
- `Angle_90_returns_right_center` — (w/2, 0) for angle 90
- `Angle_180_returns_bottom_center` — (0, h/2) for angle 180
- `Angle_270_returns_left_center` — (-w/2, 0) for angle 270
- `Angle_45_returns_top_right_corner` — for a square, (w/2, -h/2)

**EllipseShape:**
- `Angle_0_returns_top` — (0, -h/2)
- `Angle_90_returns_right` — (w/2, 0)
- `Angle_45_on_circle_returns_correct_point` — for equal w/h, verify point is on circle

### Step 3.3: Implement shapes

- [ ] Create `src/NodiumGraph/Model/RectangleShape.cs`:

```csharp
namespace NodiumGraph.Model;

using Avalonia;

public class RectangleShape : INodeShape
{
    public Point GetBoundaryPoint(double angleDegrees, double width, double height)
    {
        // Convert to radians, 0 = top (negative Y), clockwise
        var rad = angleDegrees * Math.PI / 180.0;
        var dx = Math.Sin(rad);
        var dy = -Math.Cos(rad);

        var hw = width / 2.0;
        var hh = height / 2.0;

        // Find intersection with rectangle edges
        var scaleX = Math.Abs(dx) > 1e-10 ? hw / Math.Abs(dx) : double.MaxValue;
        var scaleY = Math.Abs(dy) > 1e-10 ? hh / Math.Abs(dy) : double.MaxValue;
        var scale = Math.Min(scaleX, scaleY);

        return new Point(dx * scale, dy * scale);
    }
}
```

- [ ] Create `src/NodiumGraph/Model/EllipseShape.cs`:

```csharp
namespace NodiumGraph.Model;

using Avalonia;

public class EllipseShape : INodeShape
{
    public Point GetBoundaryPoint(double angleDegrees, double width, double height)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        var a = width / 2.0;
        var b = height / 2.0;
        // Parametric: x = a*sin(t), y = -b*cos(t) where t = rad
        return new Point(a * Math.Sin(rad), -b * Math.Cos(rad));
    }
}
```

- [ ] Create `src/NodiumGraph/Model/RoundedRectangleShape.cs`:

```csharp
namespace NodiumGraph.Model;

using Avalonia;

public class RoundedRectangleShape(double cornerRadius) : INodeShape
{
    public double CornerRadius { get; } = cornerRadius;

    public Point GetBoundaryPoint(double angleDegrees, double width, double height)
    {
        // Start with rectangle intersection, then adjust for rounded corners
        var rect = new RectangleShape();
        var point = rect.GetBoundaryPoint(angleDegrees, width, height);

        var hw = width / 2.0;
        var hh = height / 2.0;
        var r = Math.Min(CornerRadius, Math.Min(hw, hh));

        // Check if point is in a corner region and adjust to arc
        var ax = Math.Abs(point.X);
        var ay = Math.Abs(point.Y);

        if (ax > hw - r && ay > hh - r)
        {
            // In corner region — find intersection with corner arc
            var cx = Math.Sign(point.X) * (hw - r);
            var cy = Math.Sign(point.Y) * (hh - r);
            var rad = angleDegrees * Math.PI / 180.0;
            var dx = Math.Sin(rad);
            var dy = -Math.Cos(rad);

            // Ray-circle intersection from center through (dx,dy) against arc at (cx,cy) radius r
            var ox = -cx;
            var oy = -cy;
            var a = dx * dx + dy * dy;
            var b2 = ox * dx + oy * dy;
            var c = ox * ox + oy * oy - r * r;
            var disc = b2 * b2 - a * c;
            if (disc >= 0)
            {
                var t = (-b2 + Math.Sqrt(disc)) / a;
                if (t > 0) return new Point(dx * t, dy * t);
            }
        }

        return point;
    }
}
```

- [ ] Run `dotnet test --filter "FullyQualifiedName~NodeShapeTests"` — verify pass.

### Step 3.4: Add Angle, Label, LabelPlacement to Port

- [ ] Modify `Port.cs`. Add backing fields and INPC properties:

```csharp
private double _angle;
public double Angle { get => _angle; set => SetField(ref _angle, value); }

private string? _label;
public string? Label { get => _label; set => SetField(ref _label, value); }

private PortLabelPlacement? _labelPlacement;
public PortLabelPlacement? LabelPlacement { get => _labelPlacement; set => SetField(ref _labelPlacement, value); }
```

- [ ] Add Shape property to `Node.cs`:

```csharp
private INodeShape _shape = new RectangleShape();
public INodeShape Shape { get => _shape; set => SetField(ref _shape, value); }
```

- [ ] Add tests to `PortTests.cs`: Angle/Label/LabelPlacement defaults, PropertyChanged.
- [ ] Add tests to `NodeTests.cs`: Shape defaults to RectangleShape, raises PropertyChanged.
- [ ] Run `dotnet test` — verify all pass.

### Step 3.5: Write AnglePortProvider tests

- [ ] Create `tests/NodiumGraph.Tests/AnglePortProviderTests.cs`:

Key tests:
- `Ports_returns_registered_ports`
- `UpdateLayout_computes_positions_from_angles` — angle 0 on rectangle should place port at top center
- `UpdateLayout_with_ellipse_shape` — verify ellipse boundary math
- `ResolvePort_returns_nearest_within_radius`
- `DistributeEvenly_spaces_ports_equally` — 4 ports → 0, 90, 180, 270
- `Angle_change_raises_LayoutInvalidated` — changing port.Angle fires the event
- `LayoutInvalidated_fires_on_port_angle_change` — provider detects angle change, recomputes, fires event

### Step 3.6: Implement AnglePortProvider

- [ ] Create `src/NodiumGraph/Model/AnglePortProvider.cs`:

```csharp
namespace NodiumGraph.Model;

using System.ComponentModel;
using Avalonia;

public class AnglePortProvider : ILayoutAwarePortProvider
{
    private readonly List<Port> _ports = [];
    private double _lastWidth;
    private double _lastHeight;
    private INodeShape? _lastShape;
    private const double HitRadiusSq = 20.0 * 20.0;

    public IReadOnlyList<Port> Ports => _ports;

    public event Action? LayoutInvalidated;

    public AnglePortProvider(IEnumerable<Port> ports)
    {
        foreach (var port in ports)
        {
            _ports.Add(port);
            port.PropertyChanged += OnPortPropertyChanged;
        }
    }

    public Port? ResolvePort(Point position)
    {
        Port? closest = null;
        var closestDistSq = HitRadiusSq;
        foreach (var port in _ports)
        {
            var dx = position.X - port.AbsolutePosition.X;
            var dy = position.Y - port.AbsolutePosition.Y;
            var distSq = dx * dx + dy * dy;
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closest = port;
            }
        }
        return closest;
    }

    public void UpdateLayout(double width, double height, INodeShape? shape)
    {
        _lastWidth = width;
        _lastHeight = height;
        _lastShape = shape;
        RecomputeAllPositions();
    }

    public void DistributeEvenly()
    {
        if (_ports.Count == 0) return;
        var step = 360.0 / _ports.Count;
        for (var i = 0; i < _ports.Count; i++)
            _ports[i].Angle = i * step;
    }

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Port.Angle) && sender is Port port)
        {
            RecomputePosition(port);
            LayoutInvalidated?.Invoke();
        }
    }

    private void RecomputeAllPositions()
    {
        foreach (var port in _ports)
            RecomputePosition(port);
    }

    private void RecomputePosition(Port port)
    {
        if (_lastWidth <= 0 || _lastHeight <= 0) return;
        var shape = _lastShape ?? new RectangleShape();
        var centerRelative = shape.GetBoundaryPoint(port.Angle, _lastWidth, _lastHeight);
        // Convert from center-relative to top-left-relative
        port.Position = new Point(centerRelative.X + _lastWidth / 2, centerRelative.Y + _lastHeight / 2);
    }
}
```

- [ ] Run `dotnet test --filter "FullyQualifiedName~AnglePortProviderTests"` — verify pass.

### Step 3.7: Integrate UpdateLayout into canvas ArrangeOverride

- [ ] Modify `NodiumGraphCanvas.cs` ArrangeOverride (lines 1199-1222). After setting `node.Width` and `node.Height` from `container.DesiredSize`, add:

```csharp
if (node.PortProvider is ILayoutAwarePortProvider layoutProvider)
    layoutProvider.UpdateLayout(node.Width, node.Height, node.Shape);
```

- [ ] Subscribe to `LayoutInvalidated` in `AddNodeContainer`. When a node is added and its PortProvider is `ILayoutAwarePortProvider`, subscribe:

```csharp
if (node.PortProvider is ILayoutAwarePortProvider layoutProvider)
    layoutProvider.LayoutInvalidated += OnPortLayoutInvalidated;

void OnPortLayoutInvalidated() => InvalidateVisual();
```

- [ ] Unsubscribe in `RemoveNodeContainer`.
- [ ] Run `dotnet test` — verify all pass.

### Step 3.8: Render port labels in CanvasOverlay

- [ ] Add `PortLabelBrushKey` to `NodiumGraphResources.cs`:

```csharp
public const string PortLabelBrushKey = "NodiumGraphPortLabelBrush";
```

- [ ] Modify `CanvasOverlay.cs` port rendering section. After drawing each port shape, if `port.Label` is not null and `PortTemplate` is null, render the label:

```csharp
if (port.Label != null)
{
    var placement = port.LabelPlacement ?? DefaultPlacementForAngle(port.Angle);
    var text = new FormattedText(port.Label, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, new Typeface("Default"), 10, labelBrush);
    var labelPos = ComputeLabelPosition(screenPos, text, placement);
    context.DrawText(text, labelPos);
}
```

**Note:** Verify `FormattedText` constructor parameters against Avalonia 12 docs before implementation (per CLAUDE.md: always use `mcp__avalonia-docs` tools to verify Avalonia API usage).

Helper `DefaultPlacementForAngle`:
```csharp
static PortLabelPlacement DefaultPlacementForAngle(double angle)
{
    angle = ((angle % 360) + 360) % 360; // Normalize to 0-360
    return angle switch
    {
        >= 315 or < 45 => PortLabelPlacement.Below,
        >= 45 and < 135 => PortLabelPlacement.Left,
        >= 135 and < 225 => PortLabelPlacement.Above,
        _ => PortLabelPlacement.Right
    };
}
```

- [ ] Run `dotnet test` — verify all pass.

### Step 3.9: Commit

- [ ] `git add -A && git commit -m "feat: add angle-based port positioning, labels, and node shapes"`

---

## Task 4: Node Collapse (Task #9)

**Files:**
- Modify: `src/NodiumGraph/Model/Node.cs` (add IsCollapsed)
- Modify: `src/NodiumGraph/Controls/DefaultTemplates.cs` (collapsed rendering)
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs` (hide ports when collapsed)
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (block connections on collapsed, pass collapsed dims)
- Test: `tests/NodiumGraph.Tests/NodeTests.cs`

### Step 4.1: Write failing tests

- [ ] Add to `NodeTests.cs`:
  - `IsCollapsed_defaults_to_false`
  - `IsCollapsed_raises_PropertyChanged`

### Step 4.2: Add IsCollapsed to Node

- [ ] Add to `Node.cs`:

```csharp
private bool _isCollapsed;
public bool IsCollapsed { get => _isCollapsed; set => SetField(ref _isCollapsed, value); }
```

- [ ] Run `dotnet test --filter "FullyQualifiedName~NodeTests"` — verify pass.

### Step 4.3: Update DefaultTemplates for collapse

- [ ] Modify the default NodeTemplate in `DefaultTemplates.cs`. The body section should be hidden when `node.IsCollapsed == true`:

```csharp
bodyBorder.IsVisible = !node.IsCollapsed;
```

For the case where `ShowHeader == false && IsCollapsed == true`, render a minimal pill indicator:

```csharp
if (!node.ShowHeader && node.IsCollapsed)
{
    // Show minimal pill with border style
    var pill = new Border
    {
        Height = 8,
        MinWidth = 40,
        CornerRadius = new CornerRadius(4),
        Background = borderBrush,
    };
    // Use pill instead of normal content
}
```

The canvas must listen for `IsCollapsed` changes and trigger `InvalidateMeasure()` — add to the same handler as `ShowHeader`.

### Step 4.4: Hide ports when collapsed (CanvasOverlay)

- [ ] Modify `CanvasOverlay.cs` port rendering section. Before rendering ports for a node, check:

```csharp
if (node.IsCollapsed) continue; // Skip all ports for collapsed nodes
```

This applies to both port shapes and labels.

### Step 4.5: Block connections on collapsed nodes (Canvas)

- [ ] Modify `NodiumGraphCanvas.cs`:

In `HitTestPort` (line 323): skip collapsed nodes at the outer `foreach (var node in Graph.Nodes)` level, before iterating ports:

```csharp
if (node.IsCollapsed) continue;
```

In `ResolvePortForConnection` (line 359): same pattern — check `node.IsCollapsed` at the node level.

This prevents starting or completing connections on collapsed node ports.

### Step 4.6: Pass dimensions to UpdateLayout correctly

- [ ] In `ArrangeOverride`, the `UpdateLayout` call already passes `node.Width` and `node.Height`. When a node is collapsed, its template renders smaller, so `container.DesiredSize` naturally reflects the collapsed height. `node.Height` updates accordingly. `UpdateLayout` gets the collapsed dimensions automatically — no special code needed.

- [ ] Run `dotnet test` — verify all pass.

### Step 4.7: Commit

- [ ] `git add -A && git commit -m "feat: add node collapse with port hiding and connection blocking"`

---

## Task 5: Grid Snap Ghost (Task #10)

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (add ShowSnapGhost, smooth drag, ghost state)
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs` (render ghost outline)
- Test: `tests/NodiumGraph.Tests/NodiumGraphCanvasDragTests.cs`

### Step 5.1: Write failing tests

- [ ] Add to `NodiumGraphCanvasDragTests.cs`:
  - `ShowSnapGhost_defaults_to_false`
  - `ShowSnapGhost_when_active_node_does_not_jump_during_drag` — verify node X/Y follow cursor precisely during drag when ShowSnapGhost=true

### Step 5.2: Add ShowSnapGhost property

- [ ] Add StyledProperty to `NodiumGraphCanvas.cs` near the other grid properties (after line 120):

```csharp
public static readonly StyledProperty<bool> ShowSnapGhostProperty =
    AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowSnapGhost));

public bool ShowSnapGhost
{
    get => GetValue(ShowSnapGhostProperty);
    set => SetValue(ShowSnapGhostProperty, value);
}
```

### Step 5.3: Add ghost state tracking

- [ ] Add internal fields to `NodiumGraphCanvas.cs`:

```csharp
private Point? _snapGhostPosition; // World-space top-left of ghost, null = no ghost
private Size _snapGhostSize;       // Size matching dragged node(s)
```

- [ ] Add internal accessors for CanvasOverlay:

```csharp
internal Point? SnapGhostPosition => _snapGhostPosition;
internal Size SnapGhostSize => _snapGhostSize;
```

### Step 5.4: Modify drag behavior for snap ghost

- [ ] Modify `OnPointerMoved` drag section (around lines 681-685). Replace the existing snap-to-grid logic:

```csharp
if (SnapToGrid && GridSize > 0)
{
    if (ShowSnapGhost)
    {
        // Smooth drag: node follows cursor exactly
        // Ghost shows snapped position for the PRIMARY dragged node only
        // (the node the user originally clicked). In multi-select drag,
        // only one ghost is shown — the primary node's snap target.
        var snappedX = Math.Round(newX / GridSize) * GridSize;
        var snappedY = Math.Round(newY / GridSize) * GridSize;

        // Only show ghost if snapped position differs from actual
        if (Math.Abs(snappedX - newX) > 0.001 || Math.Abs(snappedY - newY) > 0.001)
        {
            _snapGhostPosition = new Point(snappedX, snappedY);
            _snapGhostSize = new Size(node.Width, node.Height);
        }
        else
        {
            _snapGhostPosition = null;
        }
        // Don't snap newX/newY — node follows cursor
    }
    else
    {
        // Existing behavior: node jumps to grid
        newX = Math.Round(newX / GridSize) * GridSize;
        newY = Math.Round(newY / GridSize) * GridSize;
    }
}
```

- [ ] On pointer release (in `OnPointerReleased` drag section): if `ShowSnapGhost` and `_snapGhostPosition != null`, snap the node to the ghost position before clearing:

```csharp
if (ShowSnapGhost && SnapToGrid && _snapGhostPosition is { } ghostPos)
{
    node.X = ghostPos.X;
    node.Y = ghostPos.Y;
}
_snapGhostPosition = null;
```

### Step 5.5: Render ghost in CanvasOverlay

- [ ] Add to `CanvasOverlay.Render`, after the selection/hover borders section and before the minimap. Check for snap ghost:

```csharp
if (_canvas.SnapGhostPosition is { } ghostWorldPos)
{
    var ghostScreenPos = transform.WorldToScreen(ghostWorldPos);
    var ghostSize = new Size(
        _canvas.SnapGhostSize.Width * transform.Zoom,
        _canvas.SnapGhostSize.Height * transform.Zoom);

    var ghostBrush = new SolidColorBrush(Colors.White, 0.3);
    var ghostPen = new Pen(ghostBrush, 2);
    context.DrawRectangle(null, ghostPen, new Rect(ghostScreenPos, ghostSize), 6, 6);
}
```

### Step 5.6: Run all tests

- [ ] Run `dotnet test` — verify all pass.

### Step 5.7: Commit

- [ ] `git add -A && git commit -m "feat: add grid snap ghost preview during node drag"`

---

## Final Verification

- [ ] Run `dotnet build` — clean build, no warnings
- [ ] Run `dotnet test` — all tests pass
- [ ] Manual smoke test in sample app: verify NodeStyle, PortStyle, ShowHeader, collapse, snap ghost all work visually
