# Write a Custom IConnectionStyle

## Goal

Replace the default grey, 2-pixel, solid connection stroke with your own brush, thickness, and dash pattern — and keep those values responding to app state like theme, zoom level, or a user preference.

## Prerequisites

- You already host `NodiumGraphCanvas`. See [Host the Canvas](host-canvas.md).
- You know how an Avalonia `Pen` is constructed (`new Pen(brush, thickness, dashStyle)`), since `IConnectionStyle` is essentially the source of those three arguments.

## Steps

### 1. The contract

```csharp
public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}
```

- Queried once per render frame. The canvas reference-compares the three getter results against its cached `Pen` and only allocates a new one when any of them changes. Mutating a style instance in place is the intended way to react to state changes — you don't have to create a fresh instance every frame.
- `Thickness` must be strictly positive; the built-in `ConnectionStyle` constructor throws on zero or negative values and you should do the same.
- `IDashStyle?` is Avalonia's `Avalonia.Media.IDashStyle`. Use `null` for a solid line or one of the static `DashStyle` presets (`DashStyle.Dash`, `DashStyle.Dot`, etc.) or a custom `new DashStyle(new double[] { 4, 2 }, 0)` for precise control.

### 2. Understand the scope

`IConnectionStyle` is **canvas-wide**. The canvas reads `NodiumGraphCanvas.DefaultConnectionStyle` once per frame and uses the resulting `Pen` for every rendered connection. There is no built-in per-connection style hook — all connections on a given canvas share the same stroke at any given moment.

That still gives you plenty of room for "data-driven" behaviour, just at a different granularity than you might expect:

- **Global-state variation works cleanly.** Theme changes (dark / light), density settings, "highlight mode", accessibility toggles, zoom-aware thickness — any input that applies to *every* connection can flow through a single style instance.
- **Per-connection variation doesn't.** If you need different colors for different connection kinds, you currently have three options: split the graph across multiple canvases, keep connections visually identical and convey kind through ports or node chrome, or fork / extend the renderer. None of these are a `IConnectionStyle` concern.

The rest of this recipe focuses on the global-state case, which is what `IConnectionStyle` is actually designed for.

### 3. Simplest case: swap the built-in style

If you just want a different look and don't need to react to anything, use `ConnectionStyle` directly:

```csharp
using Avalonia.Media;
using NodiumGraph.Interactions;

Canvas.DefaultConnectionStyle = new ConnectionStyle(
    stroke: new SolidColorBrush(Color.Parse("#475569")),
    thickness: 2.5,
    dashPattern: null);
```

### 4. Theme-aware, mutable style

For dark / light mode or any other app-wide state, implement `IConnectionStyle` with `get`-only properties that read from your theme source. Mutate the instance when the theme changes and the canvas picks it up next frame.

```csharp
using Avalonia.Media;
using NodiumGraph.Interactions;

public sealed class ThemedConnectionStyle : IConnectionStyle
{
    private IBrush _stroke = Brushes.Gray;
    private double _thickness = 2.0;

    public IBrush Stroke => _stroke;
    public double Thickness => _thickness;
    public IDashStyle? DashPattern => null;

    public void ApplyTheme(bool isDark)
    {
        _stroke = isDark
            ? new SolidColorBrush(Color.Parse("#CBD5E1"))
            : new SolidColorBrush(Color.Parse("#475569"));
        _thickness = isDark ? 2.0 : 2.5;
    }
}
```

Wire it up once, then call `ApplyTheme` whenever your theme source changes:

```csharp
var style = new ThemedConnectionStyle();
Canvas.DefaultConnectionStyle = style;

Application.Current!.ActualThemeVariantChanged += (_, _) =>
    style.ApplyTheme(Application.Current.ActualThemeVariant == ThemeVariant.Dark);

// Initial sync:
style.ApplyTheme(Application.Current.ActualThemeVariant == ThemeVariant.Dark);
```

You do not need to tell the canvas to redraw — mutating the style's backing fields changes the values returned by the getters, and the canvas's pen cache notices the change on the next render pass.

### 5. Zoom-aware thickness

If your diagrams zoom out to overview scale and connections become visually heavy, drop thickness as zoom decreases:

```csharp
public sealed class ZoomScaledStyle(NodiumGraphCanvas canvas) : IConnectionStyle
{
    public IBrush Stroke { get; } = new SolidColorBrush(Color.Parse("#64748B"));
    public IDashStyle? DashPattern => null;

    public double Thickness
    {
        get
        {
            var zoom = Math.Max(canvas.ViewportZoom, 0.25);
            return Math.Clamp(2.0 / zoom, 1.0, 3.5);
        }
    }
}
```

The `Thickness` getter runs every frame, so the width recomputes automatically as the user zooms. Because the value is a `double`, not a struct, the pen cache treats each new value as a change — acceptable overhead for something the user actively controls.

### 6. Use a dashed style

```csharp
using Avalonia.Media;

Canvas.DefaultConnectionStyle = new ConnectionStyle(
    stroke: Brushes.Gray,
    thickness: 1.5,
    dashPattern: new DashStyle(new double[] { 4, 2 }, 0));
```

The `DashStyle` array is `{dashLength, gapLength, dashLength, gapLength, ...}`, measured in **multiples of thickness** per Avalonia's convention — not absolute pixels. A pattern of `{4, 2}` at `thickness: 2` draws 8-pixel dashes separated by 4-pixel gaps.

## Full code

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;

public sealed class AppConnectionStyle : IConnectionStyle
{
    private IBrush _stroke;
    private double _thickness;
    private IDashStyle? _dashPattern;

    public AppConnectionStyle()
    {
        _stroke = Brushes.Gray;
        _thickness = 2.0;
        _dashPattern = null;
    }

    public IBrush Stroke => _stroke;
    public double Thickness => _thickness;
    public IDashStyle? DashPattern => _dashPattern;

    public void ApplyTheme(ThemeVariant theme)
    {
        var isDark = theme == ThemeVariant.Dark;
        _stroke = new SolidColorBrush(Color.Parse(isDark ? "#CBD5E1" : "#475569"));
        _thickness = isDark ? 2.0 : 2.5;
    }

    public void SetEmphasized(bool emphasized)
    {
        _dashPattern = emphasized ? null : new DashStyle(new double[] { 4, 2 }, 0);
    }
}

// Wiring in MainWindow.axaml.cs:
var style = new AppConnectionStyle();
style.ApplyTheme(Application.Current!.ActualThemeVariant);
Canvas.DefaultConnectionStyle = style;
Application.Current.ActualThemeVariantChanged += (_, _) =>
    style.ApplyTheme(Application.Current.ActualThemeVariant);
```

## Gotchas

- **`IConnectionStyle` is canvas-wide, not per-connection.** Every connection on a given canvas renders with the same pen. If you need per-kind or per-state colors for connections, this is not the right hook — consider visualising kind through the node or port templates instead.
- **Don't allocate a new `SolidColorBrush` per frame.** The canvas reference-compares the `Stroke` getter result against the cached pen — returning a new brush every call defeats the cache and re-allocates a `Pen` every render. Create brushes once and return the same instance until a real change happens.
- **`Thickness` must be positive.** The built-in `ConnectionStyle` constructor throws for zero or negative values; your own implementation should match. A zero-thickness pen renders nothing on most backends.
- **`DashStyle` lengths are in units of thickness, not pixels.** This is Avalonia convention. A pattern that looks right at `thickness = 2` will look twice as chunky at `thickness = 4`. Pre-compute arrays if you need pixel-exact dashes across thicknesses.
- **Mutating a style in place is the intended pattern.** You do not need to `Canvas.DefaultConnectionStyle = newInstance` on every update — mutate the instance's backing fields and the next render picks up the change.
- **Freshly-assigned styles take effect on the next frame.** If you call `InvalidateVisual()` right after assigning, you get an immediate redraw; otherwise Avalonia reaches it on its own schedule.

## See also

- [Strategy interfaces reference](../reference/strategies.md#iconnectionstyle)
- [Custom router](custom-router.md)
- [Theme the canvas](theme-canvas.md)
- [Rendering pipeline reference](../reference/rendering-pipeline.md)
- [NodiumGraphCanvas control reference](../reference/canvas-control.md)
