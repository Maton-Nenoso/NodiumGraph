# Grid Enhancements Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Add 5 grid enhancements: None style, configurable major interval, origin axes with per-axis colors, opacity fade on zoom, and adaptive density.

**Architecture:** All rendering changes live in `GridRenderer`. New properties on `NodiumGraphCanvas`. Theme resources in `Generic.axaml` + `NodiumGraphResources`. Sample sidebar gets new controls.

**Tech Stack:** Avalonia 12, .NET 10, xUnit v3

---

### Task 1: Add GridStyle.None

**Files:**
- Modify: `src/NodiumGraph/Controls/GridStyle.cs`
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs:10-20`
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml:28-31`

**Step 1: Add None to enum**

In `GridStyle.cs`, add `None` before `Dots`:

```csharp
public enum GridStyle
{
    None,
    Dots,
    Lines
}
```

**Step 2: Update GridRenderer.Render to handle None**

In `GridRenderer.cs`, change the dispatch:

```csharp
public static void Render(DrawingContext context, Rect bounds,
    ViewportTransform transform, double gridSize, GridStyle style,
    IBrush dotBrush, IBrush majorBrush)
{
    if (gridSize < 1.0 || style == GridStyle.None) return;

    if (style == GridStyle.Lines)
        RenderLines(context, bounds, transform, gridSize, dotBrush, majorBrush);
    else
        RenderDots(context, bounds, transform, gridSize, dotBrush);
}
```

**Step 3: Add None to sample ComboBox**

In `MainWindow.axaml`, add the None item and update SelectedIndex:

```xml
<ComboBox x:Name="GridStyleCombo" SelectedIndex="1">
    <ComboBoxItem Content="None" Tag="{x:Static ng:GridStyle.None}" />
    <ComboBoxItem Content="Dots" Tag="{x:Static ng:GridStyle.Dots}" />
    <ComboBoxItem Content="Lines" Tag="{x:Static ng:GridStyle.Lines}" />
</ComboBox>
```

**Step 4: Build and verify**

Run: `dotnet build`
Expected: 0 errors

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/GridStyle.cs src/NodiumGraph/Controls/GridRenderer.cs samples/NodiumGraph.Sample/MainWindow.axaml
git commit -m "Add GridStyle.None to disable grid rendering while keeping snap"
```

---

### Task 2: Configurable Major Line Interval

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:118-121` (add property registration)
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:207-210` (add CLR property)
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:944-948` (update render call)
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs:1051-1053` (add to invalidation)
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs` (replace constant, add parameter)
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml` (add slider)

**Step 1: Add StyledProperty to canvas**

After `GridStyleProperty`, add:

```csharp
public static readonly StyledProperty<int> MajorGridIntervalProperty =
    AvaloniaProperty.Register<NodiumGraphCanvas, int>(nameof(MajorGridInterval), 5);
```

Add CLR property after `GridStyle`:

```csharp
public int MajorGridInterval
{
    get => GetValue(MajorGridIntervalProperty);
    set => SetValue(MajorGridIntervalProperty, value);
}
```

Add `MajorGridIntervalProperty` to the `InvalidateVisual()` property-changed group (alongside `GridStyleProperty`).

**Step 2: Update GridRenderer.Render signature**

Remove the `MajorLineInterval` constant. Add `int majorInterval` parameter to `Render` and pass through to `RenderLines`:

```csharp
public static void Render(DrawingContext context, Rect bounds,
    ViewportTransform transform, double gridSize, GridStyle style,
    IBrush dotBrush, IBrush majorBrush, int majorInterval)
{
    if (gridSize < 1.0 || style == GridStyle.None) return;

    if (style == GridStyle.Lines)
        RenderLines(context, bounds, transform, gridSize, dotBrush, majorBrush, majorInterval);
    else
        RenderDots(context, bounds, transform, gridSize, dotBrush);
}
```

Update `RenderLines` to accept `int majorInterval` and use `gridSize * majorInterval` instead of `gridSize * MajorLineInterval`.

**Step 3: Update canvas render call**

```csharp
GridRenderer.Render(context, new Rect(Bounds.Size), transform, GridSize, GridStyle,
    gridBrush, majorBrush, MajorGridInterval);
```

**Step 4: Add slider to sample sidebar**

After the Snap to Grid toggle:

```xml
<StackPanel Spacing="4">
    <TextBlock Text="{Binding #MajorIntervalSlider.Value, StringFormat='Major Interval: {0:F0}'}"
               FontSize="12" />
    <Slider x:Name="MajorIntervalSlider"
            Minimum="2" Maximum="20"
            Value="{Binding #Canvas.MajorGridInterval}"
            TickFrequency="1"
            IsSnapToTickEnabled="True" />
</StackPanel>
```

**Step 5: Build and verify**

Run: `dotnet build && dotnet test`
Expected: 0 errors, all tests pass

**Step 6: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs src/NodiumGraph/Controls/GridRenderer.cs samples/NodiumGraph.Sample/MainWindow.axaml
git commit -m "Add configurable MajorGridInterval property (default 5)"
```

---

### Task 3: Origin Axes with Per-Axis Colors

**Files:**
- Modify: `src/NodiumGraph/NodiumGraphResources.cs` (add 2 resource keys)
- Modify: `src/NodiumGraph/Themes/Generic.axaml` (add 2 default brushes)
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` (add ShowOriginAxes property + 2 default brushes + render call)
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs` (add RenderOriginAxes method)
- Modify: `samples/NodiumGraph.Sample/App.axaml` (add light-theme axis brushes)
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml` (add toggle)

**Step 1: Add resource keys**

In `NodiumGraphResources.cs`, after `MajorGridBrushKey`:

```csharp
public const string OriginXAxisBrushKey = "NodiumGraphOriginXAxisBrush";
public const string OriginYAxisBrushKey = "NodiumGraphOriginYAxisBrush";
```

**Step 2: Add default brushes to Generic.axaml**

After the `MajorGridBrush` line:

```xml
<SolidColorBrush x:Key="NodiumGraphOriginXAxisBrush" Color="#60E05050" />
<SolidColorBrush x:Key="NodiumGraphOriginYAxisBrush" Color="#6050B050" />
```

**Step 3: Add ShowOriginAxes StyledProperty to canvas**

After `MajorGridIntervalProperty`:

```csharp
public static readonly StyledProperty<bool> ShowOriginAxesProperty =
    AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowOriginAxes), true);
```

CLR property:

```csharp
public bool ShowOriginAxes
{
    get => GetValue(ShowOriginAxesProperty);
    set => SetValue(ShowOriginAxesProperty, value);
}
```

Default brushes (after `DefaultMajorGridBrush`):

```csharp
internal static readonly SolidColorBrush DefaultOriginXAxisBrush = new(Color.FromArgb(96, 224, 80, 80));
internal static readonly SolidColorBrush DefaultOriginYAxisBrush = new(Color.FromArgb(96, 80, 176, 80));
```

Add `ShowOriginAxesProperty` to the invalidation group.

**Step 4: Add render call in canvas Render method**

After the `ShowGrid` block, before the `Graph != null` check:

```csharp
if (ShowOriginAxes)
{
    var xAxisBrush = ResolveBrush(NodiumGraphResources.OriginXAxisBrushKey, DefaultOriginXAxisBrush);
    var yAxisBrush = ResolveBrush(NodiumGraphResources.OriginYAxisBrushKey, DefaultOriginYAxisBrush);
    GridRenderer.RenderOriginAxes(context, new Rect(Bounds.Size), transform, xAxisBrush, yAxisBrush);
}
```

**Step 5: Implement RenderOriginAxes in GridRenderer**

```csharp
public static void RenderOriginAxes(DrawingContext context, Rect bounds,
    ViewportTransform transform, IBrush xAxisBrush, IBrush yAxisBrush)
{
    var origin = transform.WorldToScreen(new Point(0, 0));
    var xPen = new Pen(xAxisBrush, 1.5);
    var yPen = new Pen(yAxisBrush, 1.5);

    // X axis (horizontal line through Y=0)
    if (origin.Y >= bounds.Top && origin.Y <= bounds.Bottom)
        context.DrawLine(xPen, new Point(bounds.Left, origin.Y), new Point(bounds.Right, origin.Y));

    // Y axis (vertical line through X=0)
    if (origin.X >= bounds.Left && origin.X <= bounds.Right)
        context.DrawLine(yPen, new Point(origin.X, bounds.Top), new Point(origin.X, bounds.Bottom));
}
```

**Step 6: Add light-theme overrides to sample App.axaml**

After the `NodiumGraphMajorGridBrush` line:

```xml
<SolidColorBrush x:Key="NodiumGraphOriginXAxisBrush" Color="#80D04040" />
<SolidColorBrush x:Key="NodiumGraphOriginYAxisBrush" Color="#8040A040" />
```

**Step 7: Add toggle to sample sidebar**

After the Major Interval slider:

```xml
<StackPanel Spacing="4">
    <ToggleSwitch IsChecked="{Binding #Canvas.ShowOriginAxes}"
                  Content="Origin Axes"
                  OnContent="On" OffContent="Off" />
</StackPanel>
```

**Step 8: Build and verify**

Run: `dotnet build && dotnet test`
Expected: 0 errors, all tests pass

**Step 9: Commit**

```bash
git add src/NodiumGraph/NodiumGraphResources.cs src/NodiumGraph/Themes/Generic.axaml src/NodiumGraph/Controls/NodiumGraphCanvas.cs src/NodiumGraph/Controls/GridRenderer.cs samples/NodiumGraph.Sample/App.axaml samples/NodiumGraph.Sample/MainWindow.axaml
git commit -m "Add origin axes with per-axis configurable colors"
```

---

### Task 4: Grid Opacity Fade on Zoom

**Files:**
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs` (add fade logic to Render)

No new properties, no sample changes — this is automatic.

**Step 1: Add ComputeFadeOpacity helper**

In `GridRenderer`:

```csharp
private static double ComputeFadeOpacity(double zoom)
{
    if (zoom >= 0.3) return 1.0;
    if (zoom <= 0.1) return 0.0;
    return (zoom - 0.1) / 0.2; // linear interpolation 0.1..0.3 -> 0..1
}
```

**Step 2: Apply opacity in Render**

Wrap the grid rendering (not origin axes) in a PushOpacity/Pop block:

```csharp
public static void Render(DrawingContext context, Rect bounds,
    ViewportTransform transform, double gridSize, GridStyle style,
    IBrush dotBrush, IBrush majorBrush, int majorInterval)
{
    if (gridSize < 1.0 || style == GridStyle.None) return;

    var opacity = ComputeFadeOpacity(transform.Zoom);
    if (opacity <= 0.0) return;

    using (opacity < 1.0 ? context.PushOpacity(opacity) : null)
    {
        if (style == GridStyle.Lines)
            RenderLines(context, bounds, transform, gridSize, dotBrush, majorBrush, majorInterval);
        else
            RenderDots(context, bounds, transform, gridSize, dotBrush);
    }
}
```

Note: `context.PushOpacity` returns a `DrawingContext.PushedState` which is `IDisposable`. When `opacity == 1.0`, skip the push entirely by passing `null` to `using` (which is a no-op).

**Step 3: Write test for ComputeFadeOpacity**

Make it `internal` and add a test in `GridRendererTests.cs`:

```csharp
[Theory]
[InlineData(1.0, 1.0)]
[InlineData(0.5, 1.0)]
[InlineData(0.3, 1.0)]
[InlineData(0.2, 0.5)]
[InlineData(0.1, 0.0)]
[InlineData(0.05, 0.0)]
internal static double ComputeFadeOpacity(double zoom) { ... }
```

```csharp
[Theory]
[InlineData(1.0, 1.0)]
[InlineData(0.5, 1.0)]
[InlineData(0.3, 1.0)]
[InlineData(0.2, 0.5)]
[InlineData(0.1, 0.0)]
[InlineData(0.05, 0.0)]
public void ComputeFadeOpacity_returns_expected_value(double zoom, double expected)
{
    var result = GridRenderer.ComputeFadeOpacity(zoom);
    Assert.Equal(expected, result, precision: 2);
}
```

**Step 4: Build and test**

Run: `dotnet build && dotnet test`
Expected: 0 errors, all tests pass

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/GridRenderer.cs tests/NodiumGraph.Tests/GridRendererTests.cs
git commit -m "Add automatic grid opacity fade when zoom < 0.3"
```

---

### Task 5: Adaptive Grid Density

**Files:**
- Modify: `src/NodiumGraph/Controls/GridRenderer.cs` (add ComputeEffectiveGridSize, use in Render)
- Modify: `tests/NodiumGraph.Tests/GridRendererTests.cs` (add tests)

No new properties, no sample changes — automatic.

**Step 1: Add ComputeEffectiveGridSize helper**

```csharp
internal static double ComputeEffectiveGridSize(double gridSize, double zoom)
{
    var effectiveSize = gridSize;

    // Consolidate: double spacing when screen pixels between lines < 15
    while (effectiveSize * zoom < 15.0)
        effectiveSize *= 2;

    // Subdivide: halve spacing when screen pixels between lines > 60
    // but never go below the base gridSize
    while (effectiveSize * zoom > 60.0 && effectiveSize / 2 >= gridSize)
        effectiveSize /= 2;

    return effectiveSize;
}
```

**Step 2: Use in Render**

In `Render`, after the early returns, compute effective size and pass to the render methods:

```csharp
var effectiveSize = ComputeEffectiveGridSize(gridSize, transform.Zoom);

using (opacity < 1.0 ? context.PushOpacity(opacity) : null)
{
    if (style == GridStyle.Lines)
        RenderLines(context, bounds, transform, effectiveSize, dotBrush, majorBrush, majorInterval, gridSize);
    else
        RenderDots(context, bounds, transform, effectiveSize, dotBrush);
}
```

**Step 3: Update RenderLines for base-relative major lines**

`RenderLines` needs the base `gridSize` to compute major line spacing (major lines should align to `baseGridSize * majorInterval`, not `effectiveSize * majorInterval`):

```csharp
private static void RenderLines(DrawingContext context, Rect bounds,
    ViewportTransform transform, double gridSize, IBrush minorBrush, IBrush majorBrush,
    int majorInterval, double baseGridSize)
{
    var (startX, startY, endX, endY) = GetWorldRange(bounds, transform, gridSize);
    var majorSpacing = baseGridSize * majorInterval;

    var minorPen = new Pen(minorBrush, 1);
    var majorPen = new Pen(majorBrush, 1);

    for (var x = startX; x <= endX; x += gridSize)
    {
        var isMajor = IsMajor(x, majorSpacing);
        var top = transform.WorldToScreen(new Point(x, startY));
        var bottom = transform.WorldToScreen(new Point(x, endY));
        context.DrawLine(isMajor ? majorPen : minorPen, top, bottom);
    }

    for (var y = startY; y <= endY; y += gridSize)
    {
        var isMajor = IsMajor(y, majorSpacing);
        var left = transform.WorldToScreen(new Point(startX, y));
        var right = transform.WorldToScreen(new Point(endX, y));
        context.DrawLine(isMajor ? majorPen : minorPen, left, right);
    }
}
```

**Step 4: Write tests**

```csharp
[Theory]
[InlineData(20.0, 1.0, 20.0)]   // Normal zoom — no change
[InlineData(20.0, 0.5, 40.0)]   // Zoomed out — consolidate (20*0.5=10 < 15)
[InlineData(20.0, 0.2, 80.0)]   // Very zoomed out — consolidate more
[InlineData(20.0, 4.0, 20.0)]   // Zoomed in — can't subdivide below base
[InlineData(50.0, 2.0, 50.0)]   // Large grid, 2x zoom: 50*2=100 > 60, but 25*2=50 still fine
public void ComputeEffectiveGridSize_returns_expected(double gridSize, double zoom, double expected)
{
    var result = GridRenderer.ComputeEffectiveGridSize(gridSize, zoom);
    Assert.Equal(expected, result, precision: 1);
}
```

**Step 5: Build and test**

Run: `dotnet build && dotnet test`
Expected: 0 errors, all tests pass

**Step 6: Commit**

```bash
git add src/NodiumGraph/Controls/GridRenderer.cs tests/NodiumGraph.Tests/GridRendererTests.cs
git commit -m "Add adaptive grid density based on zoom level"
```
