---
title: Per-Node Port Rendering — Implementation Plan
tags: [plan]
status: active
created: 2026-04-14
updated: 2026-04-14
---

# Per-Node Port Rendering Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Move per-node decorations (selection/hover border, port shapes, port labels) out of the global `CanvasOverlay.Render()` pass and into a per-node adornment layer so that Avalonia's natural visual-tree traversal produces correct z-order overlap. Fixes the bug where a back-node's ports visually bleed over a front-node's body.

**Architecture:** New internal `NodiumNodeContainer : Panel` replaces the bare `ContentControl` currently created per-node in `NodiumGraphCanvas.AddNodeContainer`. It contains two visual children: a `ContentPresenter` for the consumer's `NodeTemplate` (child[0]) and a new `NodeAdornmentLayer : Control` (child[1]) that overrides `Render()` to draw selection border, ports, and port labels in node-local coordinates. Because Avalonia renders child[1] after child[0], per-node decorations are painted with their own node rather than in a global overlay pass. Hit-testing (`ResolvePort`) and coordinate system are unchanged.

**Tech Stack:** C# 13, .NET 10, Avalonia 12, xUnit v3.

**Design doc:** [[2026-04-14-port-rendering-zorder-design]]

---

## Important preconditions (read before starting)

1. **CanvasOverlay is already a child of the canvas visual tree** — `NodiumGraphCanvas` constructor line 31 does `VisualChildren.Add(_overlay); _overlay.ZIndex = int.MaxValue;`. It is NOT called explicitly from `Render()`. This matters for the move: we are reducing what the overlay draws, not removing the overlay.

2. **Per-container coordinate system is node-local after `ScaleTransform`** — `NodiumGraphCanvas.ArrangeOverride` sets each container's `RenderTransform = ScaleTransform(ViewportZoom, ViewportZoom)` and arranges it at the screen-position-of-node. Inside the container, coordinates are in **node-local world units** (same units as `port.Position.X` / `port.Position.Y`). The adornment layer therefore draws ports at `port.Position.X, port.Position.Y` directly — **no manual zoom scaling**. The scale transform handles zoom automatically. This is a significant simplification vs. the current `CanvasOverlay` port code which does manual `* zoom` scaling.

3. **The overlay currently owns shared caches** — `_labelCache`, `_styledPenCache`, `_portGeometryCache` on `CanvasOverlay` (src/NodiumGraph/Controls/CanvasOverlay.cs:37-47). Per-node adornment layers will share these caches via the `NodiumGraphCanvas` instance, not duplicate them per node. Task 2 hoists the caches up to the canvas.

4. **Zoom-stable strokes** — `CanvasOverlay` today draws in screen space so it uses absolute pen thickness. Inside a `NodeAdornmentLayer`, drawing happens in node-local space under a zoom scale transform. That means a pen thickness of `2` will visually become `2 * ViewportZoom` pixels. To keep strokes zoom-stable, the adornment layer must divide desired-screen-thickness by `ViewportZoom`: `penThickness = desiredScreenThickness / zoom`. This is mentioned in every task that creates a pen.

5. **Node coordinates are written back from measure** — `NodiumGraphCanvas.ArrangeOverride` writes `container.DesiredSize.Width/Height` back to `node.Width/Height`. `NodiumNodeContainer.MeasureOverride` must pass through the content's desired size so this still works.

6. **Pre-1.0 — breaking internal changes OK** — per `CLAUDE.md`. `NodiumNodeContainer` and `NodeAdornmentLayer` are `internal sealed`. No public API surface changes.

---

## Pre-flight

Confirm clean state on `main`:

```bash
git status
git log --oneline -3
```

Expected: clean tree, HEAD at `4ad7616 docs: add per-node port rendering design`.

Create a feature branch matching the workflow from the previous task:

```bash
git checkout -b feat/port-rendering-zorder
```

---

### Task 0: Scaffold empty `NodeAdornmentLayer` and `NodiumNodeContainer`

Create both new internal types as empty scaffolds — compile, can be instantiated, no behavior yet. This lets later tasks land logic into known-existing files.

**Files:**
- Create: `src/NodiumGraph/Controls/NodeAdornmentLayer.cs`
- Create: `src/NodiumGraph/Controls/NodiumNodeContainer.cs`

**Step 1: Create `NodeAdornmentLayer.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// Internal per-node adornment control. Draws selection/hover border, port shapes,
/// and port labels in node-local coordinates so that each node's decorations render
/// with that node (respecting z-order overlap) instead of in a global overlay pass.
/// Non-hit-testable; pointer input is resolved centrally by <see cref="NodiumGraphCanvas"/>.
/// </summary>
internal sealed class NodeAdornmentLayer : Control
{
    private readonly NodiumGraphCanvas _canvas;
    private readonly Node _node;

    public NodeAdornmentLayer(NodiumGraphCanvas canvas, Node node)
    {
        _canvas = canvas;
        _node = node;
        IsHitTestVisible = false;
    }

    internal Node Node => _node;

    public override void Render(DrawingContext context)
    {
        // Tasks 3–5 fill this in: selection border → port shapes → port labels.
    }
}
```

**Step 2: Create `NodiumNodeContainer.cs`**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// Internal per-node container. Replaces the bare ContentControl that used to be
/// created per node. Hosts the consumer's NodeTemplate in a ContentPresenter and
/// a NodeAdornmentLayer as a second visual child so that per-node decorations
/// render with their own node, respecting z-order overlap.
/// </summary>
internal sealed class NodiumNodeContainer : Panel
{
    private readonly ContentPresenter _contentPresenter;
    private readonly NodeAdornmentLayer _adornments;

    public NodiumNodeContainer(NodiumGraphCanvas canvas, Node node)
    {
        Node = node;
        ClipToBounds = false;
        DataContext = node;

        _contentPresenter = new ContentPresenter
        {
            Content = node,
        };
        Children.Add(_contentPresenter);

        _adornments = new NodeAdornmentLayer(canvas, node);
        Children.Add(_adornments);
    }

    internal Node Node { get; }

    internal ContentPresenter ContentPresenter => _contentPresenter;

    internal NodeAdornmentLayer AdornmentLayer => _adornments;

    internal Avalonia.Controls.Templates.IDataTemplate? ContentTemplate
    {
        get => _contentPresenter.ContentTemplate;
        set => _contentPresenter.ContentTemplate = value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _contentPresenter.Measure(availableSize);
        _adornments.Measure(_contentPresenter.DesiredSize);
        return _contentPresenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _contentPresenter.Arrange(rect);
        _adornments.Arrange(rect);
        return finalSize;
    }
}
```

**Step 3: Build**

```bash
dotnet build
```

Expected: build succeeds. No tests run yet.

**Step 4: Commit**

```bash
git add src/NodiumGraph/Controls/NodeAdornmentLayer.cs src/NodiumGraph/Controls/NodiumNodeContainer.cs
git commit -m "feat(canvas): scaffold NodiumNodeContainer and NodeAdornmentLayer"
```

---

### Task 1: Wire `NodiumNodeContainer` into `AddNodeContainer` and `_nodeContainers`

Replace the bare `ContentControl` path in `NodiumGraphCanvas` with `NodiumNodeContainer`. Adornment layer still renders nothing, so the visual output is unchanged — this task is a plumbing refactor only.

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (several touch points)

**Step 1: Change `_nodeContainers` field type**

`NodiumGraphCanvas.cs:38` currently:

```csharp
private readonly Dictionary<Node, ContentControl> _nodeContainers = new();
```

Replace with:

```csharp
private readonly Dictionary<Node, NodiumNodeContainer> _nodeContainers = new();
```

**Step 2: Rewrite `AddNodeContainer`**

`NodiumGraphCanvas.cs:1318-1354`. Replace the `ContentControl` construction with `NodiumNodeContainer`:

```csharp
private void AddNodeContainer(Node node)
{
    var template = DefaultTemplates.ResolveTemplate(node, NodeTemplate);
    var container = new NodiumNodeContainer(this, node)
    {
        ContentTemplate = template,
    };

    if (template == null)
    {
        // Custom subclass with no specific template — let DataTemplate resolution
        // walk the visual tree. Fallback templates are registered on the canvas
        // (not on each container) so that more-specific templates defined higher
        // in the tree (e.g. Window.DataTemplates) take priority.
        EnsureFallbackTemplates();
    }

    _nodeContainers[node] = container;
    LogicalChildren.Add(container);
    VisualChildren.Add(container);

    node.PropertyChanged += OnNodePropertyChanged;

    if (node.PortProvider != null)
        AttachProvider(node, node.PortProvider);

    if (node.PortProvider is ILayoutAwarePortProvider layoutAware)
        layoutAware.LayoutInvalidated += OnLayoutAwareProviderInvalidated;

    InvalidateMeasure();
}
```

**Step 3: Audit every other usage of `_nodeContainers`**

The container type is now `NodiumNodeContainer`. The following call sites currently use properties that exist on `ContentControl` but may or may not exist on `NodiumNodeContainer`. Verify each and fix compile errors as they appear:

- `NodiumGraphCanvas.cs:338` — `NodeContainerCount` returns `_nodeContainers.Count`. Unchanged.
- `NodiumGraphCanvas.cs:1184` — `OnAttachedToVisualTree` checks `.Count == 0`. Unchanged.
- `NodiumGraphCanvas.cs:1252` — `OnGraphChanged` iterates `.Values` and removes each from `LogicalChildren` / `VisualChildren`. Unchanged — still `Control` subclass.
- `NodiumGraphCanvas.cs:1258` — `.Clear()`. Unchanged.
- `NodiumGraphCanvas.cs:1283` — `foreach (var (node, container) in _nodeContainers)`. Unchanged type.
- `NodiumGraphCanvas.cs:1294` — `.Clear()`. Unchanged.
- `NodiumGraphCanvas.cs:1371, 1383` — `RemoveNodeContainer` uses `TryGetValue` and `Remove`. Unchanged (just the value type).
- `NodiumGraphCanvas.cs:1497` — `OnNodePropertyChanged` uses `TryGetValue` and (likely) touches `container.ContentTemplate`. If it does, replace with `container.ContentTemplate = ...`. If it uses `container.Content` or other ContentControl-specific members, adapt via the exposed `ContentPresenter` or `ContentTemplate` on `NodiumNodeContainer`.
- `NodiumGraphCanvas.cs:1509` — `MeasureOverride` calls `container.Measure(Size.Infinity)`. Unchanged.
- `NodiumGraphCanvas.cs:1523` — `ArrangeOverride` reads `container.DesiredSize`, sets `container.RenderTransform`, `container.RenderTransformOrigin`, calls `container.Arrange(...)`. All members inherited from `Control` / `Visual`. Unchanged.

Use Read on the exact sections before editing to catch anything not listed here. Any property-not-found compile errors at this step will point to subtleties not captured in the survey — treat them as "STOP and report back."

**Step 4: Build and run tests**

```bash
dotnet build
dotnet test
```

Expected: build succeeds. All tests pass — visual output is unchanged because `NodeAdornmentLayer.Render()` is an empty stub. Test counts stay where they are (414).

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "feat(canvas): use NodiumNodeContainer for per-node hosting"
```

---

### Task 2: Hoist shared render caches from `CanvasOverlay` to `NodiumGraphCanvas`

Move the label / styled-pen / port-geometry caches and their helper methods from `CanvasOverlay` onto `NodiumGraphCanvas` so both the overlay AND the new `NodeAdornmentLayer` can share them via the canvas instance. Pure refactor — no behavior change.

**Files:**
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs`
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`

**Step 1: Move the cache fields and helpers from `CanvasOverlay` onto `NodiumGraphCanvas`**

From `CanvasOverlay.cs`, move these fields (lines 37-47) and their helper methods (lines 55-115) into `NodiumGraphCanvas` as `internal` members:

- `_labelCache` + `GetOrCreateLabel(string, double bucketedFontSize, IBrush)` helper (extract from the inline logic in the current port-label rendering section)
- `_styledPenCache` + `GetOrCreateStyledPen(IBrush, double thickness)` (CanvasOverlay.cs:68-80)
- `_portGeometryCache` + `GetOrCreatePortGeometry(PortShape, double bucketedRadius)` (CanvasOverlay.cs:82-115)
- The per-field cached pens (`_cachedSelectedBorderPen`, `_cachedHoveredBorderPen`, `_cachedPortOutlinePen`) and their single-value cache invalidation logic — move as internal methods `GetSelectedBorderPen(brush, thickness)`, `GetHoveredBorderPen(brush, thickness)`, `GetPortOutlinePen(brush, thickness)` on the canvas.

Place them near the top of `NodiumGraphCanvas.cs` alongside other internal rendering helpers, marked `internal`:

```csharp
// Shared render caches — used by CanvasOverlay (validation feedback, previews)
// and by per-node NodeAdornmentLayer (selection border, ports, labels).

private const int LabelCacheMaxEntries = 256;
private readonly Dictionary<(string label, double bucketedFontSize, IBrush brush), FormattedText> _labelCache
    = new(LabelCacheKeyComparer.Instance);

private const int StyledPenCacheMaxEntries = 32;
private readonly Dictionary<(IBrush brush, double thickness), Pen> _styledPenCache
    = new(BrushThicknessComparer.Instance);

private const int PortGeometryCacheMaxEntries = 64;
private readonly Dictionary<(PortShape shape, double bucketedRadius), Geometry> _portGeometryCache = new();

private Pen? _cachedSelectedBorderPen;
private IBrush? _lastSelectedBrush;
private double _lastSelectedThickness;

private Pen? _cachedHoveredBorderPen;
private IBrush? _lastHoveredBrush;
private double _lastHoveredThickness;

private Pen? _cachedPortOutlinePen;
private IBrush? _lastPortOutlineBrush;
private double _lastPortOutlineThickness;

internal Pen GetOrCreateStyledPen(IBrush brush, double thickness)
{
    // body from CanvasOverlay.cs:68-80
}

internal Geometry GetOrCreatePortGeometry(PortShape shape, double bucketedRadius)
{
    // body from CanvasOverlay.cs:82-115
}

internal FormattedText GetOrCreateLabel(string text, double bucketedFontSize, IBrush brush)
{
    var key = (text, bucketedFontSize, brush);
    if (!_labelCache.TryGetValue(key, out var ft))
    {
        if (_labelCache.Count >= LabelCacheMaxEntries)
            _labelCache.Clear();

        ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            bucketedFontSize,
            brush);
        _labelCache[key] = ft;
    }
    return ft;
}

internal Pen GetSelectedBorderPen(IBrush brush, double thickness)
{
    if (!ReferenceEquals(_lastSelectedBrush, brush) || _lastSelectedThickness != thickness)
    {
        _cachedSelectedBorderPen = new Pen(brush, thickness);
        _lastSelectedBrush = brush;
        _lastSelectedThickness = thickness;
    }
    return _cachedSelectedBorderPen!;
}

internal Pen GetHoveredBorderPen(IBrush brush, double thickness)
{
    if (!ReferenceEquals(_lastHoveredBrush, brush) || _lastHoveredThickness != thickness)
    {
        _cachedHoveredBorderPen = new Pen(brush, thickness);
        _lastHoveredBrush = brush;
        _lastHoveredThickness = thickness;
    }
    return _cachedHoveredBorderPen!;
}

internal Pen GetPortOutlinePen(IBrush brush, double thickness)
{
    if (!ReferenceEquals(_lastPortOutlineBrush, brush) || _lastPortOutlineThickness != thickness)
    {
        _cachedPortOutlinePen = new Pen(brush, thickness);
        _lastPortOutlineBrush = brush;
        _lastPortOutlineThickness = thickness;
    }
    return _cachedPortOutlinePen!;
}
```

**Step 2: Update `CanvasOverlay` to call into `_canvas` for caches**

Replace every reference to the moved fields and helpers with the equivalent `_canvas.GetOrCreate...` call. Delete the now-orphaned field declarations and helper methods from `CanvasOverlay.cs`.

The inline `FormattedText` creation in the port-label rendering section (`CanvasOverlay.cs:298-312`) becomes `var text = _canvas.GetOrCreateLabel(port.Label, bucketedFontSize, portLabelBrush);` — the label cache is no longer needed inline.

**Step 3: Build and run tests**

```bash
dotnet build
dotnet test
```

Expected: build succeeds, 414/414 pass. Pure refactor — no visual change.

**Step 4: Commit**

```bash
git add src/NodiumGraph/Controls/CanvasOverlay.cs src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "refactor(canvas): hoist shared render caches to NodiumGraphCanvas"
```

---

### Task 3: Move selection border + hover border into `NodeAdornmentLayer`

Move the node-state border rendering (`CanvasOverlay.cs:175-202`) into `NodeAdornmentLayer.Render`, rewire selection/hover invalidation to hit the per-container adornment layer, delete the code from `CanvasOverlay`.

**Files:**
- Modify: `src/NodiumGraph/Controls/NodeAdornmentLayer.cs`
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs`
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`

**Step 1: Implement selection/hover border in `NodeAdornmentLayer.Render`**

Rewrite `NodeAdornmentLayer.Render` to:

```csharp
public override void Render(DrawingContext context)
{
    var zoom = _canvas.ViewportZoom;
    if (zoom <= 0) return;

    var isSelected = _node.IsSelected;
    var isHovered = !isSelected && ReferenceEquals(_canvas.HoveredNode, _node);
    if (!isSelected && !isHovered) return;

    // Node-local rect inflated by 2 world units, matching the previous overlay behavior
    // which inflated by 2 screen-space units (since zoom=1 was the default). Use 2/zoom
    // so the border stays visually 2px regardless of zoom.
    var inflate = 2.0 / zoom;
    var rect = new Rect(0, 0, _node.Width, _node.Height).Inflate(inflate);

    // Zoom-stable stroke thickness: drawing is in node-local space under a zoom
    // scale transform, so divide desired-screen-thickness by zoom.
    var defaultSelectedBrush = _canvas.ResolveBrush(
        NodiumGraphResources.NodeSelectedBorderBrushKey,
        NodiumGraphCanvas.DefaultSelectedBorderBrush);
    var defaultSelectedThickness = _canvas.ResolveResource<double>(
        NodiumGraphResources.NodeSelectedBorderThicknessKey, 2);

    var defaultHoveredBrush = _canvas.ResolveBrush(
        NodiumGraphResources.NodeHoveredBorderBrushKey,
        NodiumGraphCanvas.DefaultHoveredBorderBrush);
    var defaultHoveredThickness = _canvas.ResolveResource<double>(
        NodiumGraphResources.NodeHoveredBorderThicknessKey, 2);

    if (isSelected)
    {
        var brush = _node.Style?.SelectionBorderBrush ?? defaultSelectedBrush;
        var thickness = (_node.Style?.SelectionBorderThickness ?? defaultSelectedThickness) / zoom;
        var pen = _canvas.GetOrCreateStyledPen(brush, thickness);
        context.DrawRectangle(null, pen, rect, 6 / zoom, 6 / zoom);
    }
    else
    {
        var brush = _node.Style?.HoverBorderBrush ?? defaultHoveredBrush;
        var thickness = (_node.Style?.HoverBorderThickness ?? defaultHoveredThickness) / zoom;
        var pen = _canvas.GetOrCreateStyledPen(brush, thickness);
        context.DrawRectangle(null, pen, rect, 6 / zoom, 6 / zoom);
    }
}
```

Note: `ResolveBrush` and `ResolveResource<T>` are currently private/internal on the overlay or canvas. If they're on the overlay, promote them to `internal` on `NodiumGraphCanvas`. If they're already on the canvas, use as-is. Adjust visibility as needed.

**Step 2: Remove the border code from `CanvasOverlay.Render`**

Delete lines `CanvasOverlay.cs:175-202` (the "node state borders" section and its resource-resolution setup for `defaultSelectedBrush`, `defaultSelectedThickness`, `defaultHoveredBrush`, `defaultHoveredThickness` if those resolutions are no longer used elsewhere in `CanvasOverlay.Render`). Leave resource resolutions that are still used by port/label code — they will be removed in Tasks 4–5.

**Step 3: Rewire selection and hover invalidation**

Replace each `InvalidateVisual()` call that's triggered by selection/hover changes with a targeted per-container invalidation:

New helper on `NodiumGraphCanvas`:

```csharp
internal void InvalidateNodeAdornments(Node node)
{
    if (_nodeContainers.TryGetValue(node, out var container))
        container.AdornmentLayer.InvalidateVisual();
}
```

Update these sites (from the survey):

- `NodiumGraphCanvas.cs:423` — `SelectNode`: after the Select/Deselect call, invalidate the affected node's adornments. If `additive` was false and multiple nodes were cleared, also invalidate every previously-selected node. Simplest: invalidate every node that changed state. Concrete rewrite:

    ```csharp
    internal void SelectNode(Node node, bool additive)
    {
        if (Graph is null) return;

        if (!additive)
        {
            foreach (var n in Graph.SelectedNodes.ToList())
            {
                Graph.Deselect(n);
                InvalidateNodeAdornments(n);
            }
        }

        if (node.IsSelected && additive)
            Graph.Deselect(node);
        else
            Graph.Select(node);

        InvalidateNodeAdornments(node);
        SelectionHandler?.OnSelectionChanged(Graph.SelectedNodes);
    }
    ```

- `NodiumGraphCanvas.cs:432` — `ClearSelection`: iterate nodes that were selected, invalidate each adornment layer. No whole-canvas invalidation needed.

- `NodiumGraphCanvas.cs:443` — `SelectAll`: invalidate every node's adornment layer (they all changed state). Still cheap since adornment layers are per-node.

- `NodiumGraphCanvas.cs:1470` — `OnNodePropertyChanged` for `IsSelected`: `InvalidateNodeAdornments(node)` instead of `InvalidateVisual()`.

- `NodiumGraphCanvas.cs:1486` — `OnNodePropertyChanged` for `PortProvider` change: still needs invalidation, but target the adornment layer — `InvalidateNodeAdornments(node)`. Leave as `InvalidateVisual()` if the port provider change also affects measure/arrange; in that case add an `InvalidateMeasure()` too.

- `HoveredNode` setter (look it up — survey didn't pin the exact line): when the hovered node changes, invalidate adornments on both the old hovered node AND the new hovered node. Otherwise the hover border stays stale.

    ```csharp
    // Pseudocode — adapt to the real setter location
    private Node? _hoveredNode;
    public Node? HoveredNode
    {
        get => _hoveredNode;
        internal set
        {
            if (ReferenceEquals(_hoveredNode, value)) return;
            var old = _hoveredNode;
            _hoveredNode = value;
            if (old != null) InvalidateNodeAdornments(old);
            if (value != null) InvalidateNodeAdornments(value);
        }
    }
    ```

**Step 4: Build and run tests**

```bash
dotnet build
dotnet test
```

Expected: build succeeds, 414/414 pass. Selection-related tests (if any) still pass.

**Step 5: Manual sanity check**

Run `samples/NodiumGraph.Sample` (or `samples/GettingStarted`). Click a node — selection border should appear. Click another — the first one should deselect and the second should show selected. Hover a node — hover border should appear. Move the mouse away — hover border gone.

**Step 6: Commit**

```bash
git add src/NodiumGraph/Controls/NodeAdornmentLayer.cs \
        src/NodiumGraph/Controls/CanvasOverlay.cs \
        src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "refactor(canvas): move selection/hover border to NodeAdornmentLayer"
```

---

### Task 4: Move port shape rendering into `NodeAdornmentLayer`

Move the port shape rendering (`CanvasOverlay.cs:213-274`) into `NodeAdornmentLayer.Render`, delete from overlay, update port-related invalidation.

**Files:**
- Modify: `src/NodiumGraph/Controls/NodeAdornmentLayer.cs`
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs`
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (port provider change handlers)

**Step 1: Append port-shape rendering to `NodeAdornmentLayer.Render`**

After the selection/hover border block, add:

```csharp
if (_node.PortProvider == null) return;
if (_node.IsCollapsed) return;

var defaultPortBrush = _canvas.ResolveBrush(
    NodiumGraphResources.PortFillBrushKey,
    NodiumGraphCanvas.DefaultPortFillBrush);
var defaultPortOutlineBrush = _canvas.ResolveBrush(
    NodiumGraphResources.PortOutlineBrushKey,
    NodiumGraphCanvas.DefaultPortOutlineBrush);
var defaultPortOutlineThickness = _canvas.ResolveResource<double>(
    NodiumGraphResources.PortOutlineThicknessKey, 1.5);

foreach (var port in _node.PortProvider.Ports)
{
    var fill = port.Style?.FillBrush ?? defaultPortBrush;
    var outlineBrush = port.Style?.OutlineBrush ?? defaultPortOutlineBrush;
    var outlineThickness = (port.Style?.OutlineThickness ?? defaultPortOutlineThickness) / zoom;
    var pen = _canvas.GetPortOutlinePen(outlineBrush, outlineThickness);

    var radius = (port.Style?.Size ?? NodiumGraphCanvas.DefaultPortSize) / 2;
    var shape = port.Style?.Shape ?? PortShape.Circle;
    var center = port.Position; // node-local

    switch (shape)
    {
        case PortShape.Circle:
            context.DrawEllipse(fill, pen, center, radius, radius);
            break;
        case PortShape.Square:
            context.DrawRectangle(fill, pen,
                new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
            break;
        case PortShape.Diamond:
        case PortShape.Triangle:
        {
            var bucketedRadius = Math.Round(radius * 2) / 2;
            var geo = _canvas.GetOrCreatePortGeometry(shape, bucketedRadius);
            using (context.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.DrawGeometry(fill, pen, geo);
            }
            break;
        }
    }
}
```

Key differences from the old overlay code:
- `center = port.Position` (node-local), not `screenPos = transform.WorldToScreen(port.AbsolutePosition)`.
- `radius = port.Style.Size / 2` (unscaled), not `scaledRadius = radius * zoom`. The scale transform handles zoom automatically.
- Pen thickness divided by zoom to stay visually stable.
- Exact default resource keys (`NodiumGraphResources.PortFillBrushKey`, etc.) must match the keys the overlay currently reads. Look them up in the overlay code and preserve them verbatim.

**Step 2: Delete port-shape code from `CanvasOverlay.cs`**

Delete `CanvasOverlay.cs:213-274`. Also delete any default-port-brush / outline resolution setup in the overlay render method that is no longer used by what's left (validation feedback may still need them — keep what's still referenced).

**Step 3: Wire per-node port invalidation**

Any place in `NodiumGraphCanvas` that currently calls `InvalidateVisual()` or `_overlay.InvalidateVisual()` in response to a port-provider change, a port-style change, or a port-collection change should instead call `InvalidateNodeAdornments(node)` for the affected node. Survey sites:

- `AttachProvider` / `DetachProvider` event handlers — when the provider raises its "ports changed" event, invalidate the adornments of the node whose provider raised the event.
- `OnLayoutAwareProviderInvalidated` — same treatment.
- `OnNodePropertyChanged` for `PortProvider`: already covered in Task 3; ensure it uses `InvalidateNodeAdornments`.

**Step 4: Build and run tests**

```bash
dotnet build
dotnet test
```

Expected: build succeeds, 414/414 pass.

**Step 5: Manual sanity check**

Run the sample. Ports appear on every node. Drag a port — the connection drag preview still works (handled by overlay; unchanged). Confirm visually that port shapes render per-node.

**Step 6: Commit**

```bash
git add src/NodiumGraph/Controls/NodeAdornmentLayer.cs \
        src/NodiumGraph/Controls/CanvasOverlay.cs \
        src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "refactor(canvas): move port shapes to NodeAdornmentLayer"
```

---

### Task 5: Move port labels into `NodeAdornmentLayer`

Move the port label rendering (`CanvasOverlay.cs:276-349`) into the adornment layer, delete from overlay.

**Files:**
- Modify: `src/NodiumGraph/Controls/NodeAdornmentLayer.cs`
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs`

**Step 1: Append port-label rendering to `NodeAdornmentLayer.Render`**

After the port-shape loop, add:

```csharp
var defaultLabelFontSize = _canvas.ResolveResource<double>(
    NodiumGraphResources.PortLabelFontSizeKey, 11);
var defaultLabelBrush = _canvas.ResolveBrush(
    NodiumGraphResources.PortLabelBrushKey,
    NodiumGraphCanvas.DefaultPortLabelBrush);
var defaultLabelOffset = _canvas.ResolveResource<double>(
    NodiumGraphResources.PortLabelOffsetKey, 4);

foreach (var port in _node.PortProvider.Ports)
{
    if (string.IsNullOrEmpty(port.Label)) continue;

    var labelFontSize = port.Style?.LabelFontSize ?? defaultLabelFontSize;
    var labelBrush = port.Style?.LabelBrush ?? defaultLabelBrush;
    var labelOffset = port.Style?.LabelOffset ?? defaultLabelOffset;
    var placement = port.Style?.LabelPlacement ?? GetAutoPlacement(port, _node);

    // Font size stays bucketed to hit the cache. Since we're in node-local space
    // under a zoom scale transform, the actual rendered size will be
    // labelFontSize * zoom — which is what we want. But the FormattedText
    // must be constructed at the UNSCALED size (labelFontSize), since the
    // scale transform handles the zoom. Bucket accordingly.
    var bucketedFontSize = Math.Round(labelFontSize * 2) / 2;
    var text = _canvas.GetOrCreateLabel(port.Label!, bucketedFontSize, labelBrush);

    var textWidth = text.Width;
    var textHeight = text.Height;

    Point textOrigin;
    switch (placement)
    {
        case PortLabelPlacement.Left:
            textOrigin = new Point(
                port.Position.X - labelOffset - textWidth,
                port.Position.Y - textHeight / 2);
            break;
        case PortLabelPlacement.Right:
            textOrigin = new Point(
                port.Position.X + labelOffset,
                port.Position.Y - textHeight / 2);
            break;
        case PortLabelPlacement.Above:
            textOrigin = new Point(
                port.Position.X - textWidth / 2,
                port.Position.Y - labelOffset - textHeight);
            break;
        case PortLabelPlacement.Below:
        default:
            textOrigin = new Point(
                port.Position.X - textWidth / 2,
                port.Position.Y + labelOffset);
            break;
    }

    context.DrawText(text, textOrigin);
}
```

Plus a private static helper at the bottom of the class:

```csharp
private static PortLabelPlacement GetAutoPlacement(Port port, Node node)
{
    var nodeCenter = node.Width / 2.0;
    return port.Position.X >= nodeCenter ? PortLabelPlacement.Right : PortLabelPlacement.Left;
}
```

Key differences from the overlay version:
- `port.Position.X/Y` node-local, not screen-space — scale transform handles zoom.
- `labelOffset` used unscaled (scale transform handles it).
- Font size bucketing uses the unscaled label size. Important: the scale transform will visually magnify the text by `zoom`, so `FormattedText` is built at the unscaled size. This will cause text to rescale smoothly at any zoom — a BETTER experience than the old overlay which re-bucketed at every zoom change.

**Step 2: Delete port-label code from `CanvasOverlay.cs`**

Delete `CanvasOverlay.cs:276-349` and the `GetAutoPlacement` helper (`CanvasOverlay.cs:464-468`) if it's not referenced elsewhere in the overlay.

**Step 3: Build and run tests**

```bash
dotnet build
dotnet test
```

Expected: 414/414 pass.

**Step 4: Manual sanity check**

Run the sample with ports that have labels (the `GettingStarted` sample's `MathNode` has labeled ports). Labels appear in the correct placement. Zoom in/out — labels scale smoothly with the node (they will now scale continuously instead of step-bucket; this is an improvement but visually different from before at extreme zoom).

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodeAdornmentLayer.cs \
        src/NodiumGraph/Controls/CanvasOverlay.cs
git commit -m "refactor(canvas): move port labels to NodeAdornmentLayer"
```

---

### Task 6: Structural tests

Add tests that pin the new architecture so refactors don't accidentally revert it.

**Files:**
- Create: `tests/NodiumGraph.Tests/NodeAdornmentLayerTests.cs`
- Modify: `tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs` (extend existing container-shape tests)

**Step 1: Add `NodeAdornmentLayerTests.cs`**

```csharp
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeAdornmentLayerTests
{
    [AvaloniaFact]
    public void NodiumNodeContainer_contains_content_presenter_and_adornment_layer()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        var node = new Node();
        canvas.Graph!.AddNode(node);

        var container = canvas.GetInternalNodeContainer(node);
        Assert.NotNull(container);
        Assert.IsType<NodiumNodeContainer>(container);
        Assert.Equal(2, container!.Children.Count);
        Assert.IsType<ContentPresenter>(container.Children[0]);
        Assert.IsType<NodeAdornmentLayer>(container.Children[1]);
    }

    [AvaloniaFact]
    public void NodeAdornmentLayer_is_not_hit_test_visible()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        var node = new Node();
        canvas.Graph!.AddNode(node);

        var container = canvas.GetInternalNodeContainer(node)!;
        var adornments = container.Children.OfType<NodeAdornmentLayer>().Single();

        Assert.False(adornments.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void Adornment_layer_is_the_last_visual_child_of_its_container()
    {
        var canvas = new NodiumGraphCanvas { Graph = new Graph() };
        var node = new Node();
        canvas.Graph!.AddNode(node);

        var container = canvas.GetInternalNodeContainer(node)!;
        Assert.IsType<NodeAdornmentLayer>(container.Children[^1]);
    }
}
```

These tests assume a new internal helper `NodiumGraphCanvas.GetInternalNodeContainer(Node)` exposed via `InternalsVisibleTo NodiumGraph.Tests`. Add it:

```csharp
internal NodiumNodeContainer? GetInternalNodeContainer(Node node)
    => _nodeContainers.TryGetValue(node, out var c) ? c : null;
```

**Step 2: Run the new tests**

```bash
dotnet test --filter "FullyQualifiedName~NodeAdornmentLayerTests"
```

Expected: 3 passed. If any fail, the architecture assertion the test encodes is wrong — fix the code, not the test.

**Step 3: Run the full suite**

```bash
dotnet test
```

Expected: 417/417 (414 + 3 new).

**Step 4: Commit**

```bash
git add tests/NodiumGraph.Tests/NodeAdornmentLayerTests.cs \
        src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "test(canvas): pin per-node adornment layer structure"
```

---

### Task 7: Manual verification and memory update

**Step 1: Reproduce the original bug in the sample**

Run `samples/NodiumGraph.Sample`. Create or load a scene with two nodes. Drag one node so that it partially overlaps another. The **foreground** node should completely cover the back node's body AND its ports — no red-circle bleed-through.

Compare against the original screenshot (`C:\Users\metro\Pictures\Screenshots\Capture d'écran 2026-04-14 152220.png`). Bug should be gone.

**Step 2: Verify hit-testing still works on covered ports**

(Expected behavior per design: a back-node port is clickable even when visually hidden, because `ResolvePort` is render-independent. This is arguably wrong but out of scope — noted as deferred in the design doc.)

Attempt to start a connection drag from a visually-covered port. It should still work. If it does NOT work, something went wrong during the move — investigate.

**Step 3: Verify zoom-stable strokes and labels**

Zoom in to 2x, 4x. Selection border stroke should stay the same visual thickness (not thicker). Port outlines should stay the same. Port labels should scale smoothly with the node body (this is actually a change — previously labels re-bucketed discretely; now they scale continuously with the node).

**Step 4: Update memory file**

Rewrite `C:\Users\metro\.claude\projects\D--Projects-Nenoso-NodiumGraph\memory\project_nodiumgraph.md` to note the rendering architecture change if it's in scope, OR add a new memory file `memory/project_port_rendering.md` documenting:
- Per-node decorations now live in `NodeAdornmentLayer` (selection/hover border, port shapes, port labels)
- Canvas chrome (validation, preview, cutting, marquee, minimap) stays in `CanvasOverlay`
- Shared render caches live on `NodiumGraphCanvas` and are used by both
- Deferred: hit-test z-order, port shape customization interface

Update `memory/MEMORY.md` index with a one-liner.

**Step 5: No git commit for memory** — memory files live outside the repo.

---

### Task 8: Final verification and push decision

**Step 1: Confirm clean state**

```bash
git status
git log --oneline main..feat/port-rendering-zorder
```

Expected: clean tree, 6 new commits on `feat/port-rendering-zorder`.

**Step 2: Run the full suite one final time**

```bash
dotnet test
```

Expected: 417 passed, 0 failed.

**Step 3: Merge to main and push**

Ask the user whether to merge and push. Do not push without confirmation.

If confirmed, follow the same pattern as the previous feature:

```bash
git checkout main
git merge --ff-only feat/port-rendering-zorder
git push origin main
git branch -d feat/port-rendering-zorder
```

---

## Acceptance criteria

- [ ] The original screenshot scenario no longer reproduces — overlapping nodes' ports do not bleed through.
- [ ] Selection border, hover border, port shapes, and port labels are drawn by `NodeAdornmentLayer.Render`, not `CanvasOverlay.Render`.
- [ ] `CanvasOverlay.Render` still owns validation feedback, connection drag preview, cutting line, marquee, and minimap.
- [ ] Shared render caches (`_labelCache`, `_styledPenCache`, `_portGeometryCache`, cached pens) live on `NodiumGraphCanvas` as internal members.
- [ ] `_nodeContainers` value type is `NodiumNodeContainer`; the dictionary is fully consistent with this type.
- [ ] `NodeAdornmentLayer.IsHitTestVisible == false`.
- [ ] Hit-testing (`ResolvePort` / `ResolvePortWithProvider`) is unchanged and still finds ports on any node regardless of visual coverage.
- [ ] Full test suite passes: 417/417.
- [ ] 6 commits on `main` (or on the feature branch ready to merge):
  1. `feat(canvas): scaffold NodiumNodeContainer and NodeAdornmentLayer`
  2. `feat(canvas): use NodiumNodeContainer for per-node hosting`
  3. `refactor(canvas): hoist shared render caches to NodiumGraphCanvas`
  4. `refactor(canvas): move selection/hover border to NodeAdornmentLayer`
  5. `refactor(canvas): move port shapes to NodeAdornmentLayer`
  6. `refactor(canvas): move port labels to NodeAdornmentLayer`
  7. `test(canvas): pin per-node adornment layer structure` (7th)
- [ ] Memory file updated.
