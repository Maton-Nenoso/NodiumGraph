# Theme the Canvas

## Goal

Customise the canvas chrome — grid color, origin axes, selection marquee, node and port brushes, minimap, connection-preview feedback — without subclassing or forking the library. The entire palette is driven by named Avalonia resources that you override in your own application's resource dictionary.

## Prerequisites

- You already host `NodiumGraphCanvas`. See [Host the Canvas](host-canvas.md).
- You're familiar with Avalonia's resource system (`Application.Resources`, `Window.Resources`, `DynamicResource`).

## Steps

### 1. How NodiumGraph resolves its colors

The library never hardcodes its visible brushes. Every chrome element looks up a brush by resource key through Avalonia's normal `DynamicResource` lookup:

1. The canvas asks the resource tree for a given key (e.g. `"NodiumGraphGridBrush"`).
2. Avalonia walks outward from the canvas — parent control → window → application — until it finds a match.
3. If no match is found, the canvas falls back to a built-in default brush.

The library ships a default set of brushes through its `ControlTheme` in `src/NodiumGraph/Themes/Generic.axaml`. Overriding a key in any enclosing resource dictionary replaces that default for every canvas in that scope.

### 2. The resource keys

All keys are constants on the static `NodiumGraph.NodiumGraphResources` class (namespace: `NodiumGraph`), so you can reference them from C# without typing the string literal. They also exist as plain string keys for AXAML use.

| Key constant | String value | Used by |
|---|---|---|
| `GridBrushKey` | `NodiumGraphGridBrush` | Minor grid dots or lines |
| `MajorGridBrushKey` | `NodiumGraphMajorGridBrush` | Every Nth cell (`MajorGridInterval`) |
| `OriginXAxisBrushKey` | `NodiumGraphOriginXAxisBrush` | Horizontal axis through world `(0, 0)` |
| `OriginYAxisBrushKey` | `NodiumGraphOriginYAxisBrush` | Vertical axis through world `(0, 0)` |
| `PortBrushKey` | `NodiumGraphPortBrush` | Default port fill |
| `PortOutlineBrushKey` | `NodiumGraphPortOutlineBrush` | Default port stroke |
| `PortLabelBrushKey` | `NodiumGraphPortLabelBrush` | Port label text |
| `NodeSelectedBorderBrushKey` | `NodiumGraphNodeSelectedBorderBrush` | Border around selected nodes |
| `NodeHoveredBorderBrushKey` | `NodiumGraphNodeHoveredBorderBrush` | Border on pointer hover |
| `MarqueeFillBrushKey` | `NodiumGraphMarqueeFillBrush` | Rubber-band selection fill |
| `MarqueeBorderBrushKey` | `NodiumGraphMarqueeBorderBrush` | Rubber-band selection border |
| `ConnectionPreviewValidBrushKey` | `NodiumGraphConnectionPreviewValidBrush` | Drag preview when validator says yes |
| `ConnectionPreviewInvalidBrushKey` | `NodiumGraphConnectionPreviewInvalidBrush` | Drag preview when validator says no |
| `CuttingLineBrushKey` | `NodiumGraphCuttingLineBrush` | Right-drag slice gesture line |
| `NodeHeaderBrushKey` | `NodiumGraphNodeHeaderBrush` | Default node header background |
| `NodeHeaderForegroundBrushKey` | `NodiumGraphNodeHeaderForegroundBrush` | Default node header text |
| `NodeBodyBrushKey` | `NodiumGraphNodeBodyBrush` | Default node body fill |
| `NodeBorderBrushKey` | `NodiumGraphNodeBorderBrush` | Default node border |
| `MinimapBackgroundBrushKey` | `NodiumGraphMinimapBackgroundBrush` | Minimap backdrop |
| `MinimapNodeBrushKey` | `NodiumGraphMinimapNodeBrush` | Minimap node blocks |
| `MinimapSelectedNodeBrushKey` | `NodiumGraphMinimapSelectedNodeBrush` | Selected nodes in the minimap |
| `MinimapViewportBrushKey` | `NodiumGraphMinimapViewportBrush` | Viewport rectangle overlay |

There are also a handful of non-brush keys for sizing and typography — `NodeHeaderFontSizeKey`, `NodeHeaderFontWeightKey`, `NodeHeaderFontFamilyKey`, `NodeHeaderPaddingKey`, `NodeBodyMinHeightKey`, `NodeMinWidthKey`, `NodeSelectedBorderThicknessKey`, `NodeHoveredBorderThicknessKey`, `PortLabelFontSizeKey`, `PortLabelOffsetKey`. Override them the same way.

### 3. Override from `App.axaml`

This is the most common path — the resources apply to every `NodiumGraphCanvas` in the whole application.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyApp.App">
  <Application.Resources>
    <ResourceDictionary>
      <!-- Cool slate theme -->
      <SolidColorBrush x:Key="NodiumGraphGridBrush" Color="#CBD5E1" />
      <SolidColorBrush x:Key="NodiumGraphMajorGridBrush" Color="#94A3B8" />
      <SolidColorBrush x:Key="NodiumGraphOriginXAxisBrush" Color="#EF4444" />
      <SolidColorBrush x:Key="NodiumGraphOriginYAxisBrush" Color="#10B981" />

      <SolidColorBrush x:Key="NodiumGraphMarqueeFillBrush" Color="#2099C1FF" />
      <SolidColorBrush x:Key="NodiumGraphMarqueeBorderBrush" Color="#0EA5E9" />

      <SolidColorBrush x:Key="NodiumGraphConnectionPreviewValidBrush" Color="#10B981" />
      <SolidColorBrush x:Key="NodiumGraphConnectionPreviewInvalidBrush" Color="#EF4444" />
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

Every canvas in the app now uses your overrides. You do **not** have to tell the canvas about the change — it reads through `DynamicResource` on every render that touches that chrome element.

### 4. Override from a single `Window` or `UserControl`

Place the same resources in a `Window.Resources` block instead to limit the override to one window:

```xml
<Window ...>
  <Window.Resources>
    <SolidColorBrush x:Key="NodiumGraphGridBrush" Color="#2A2A2A" />
    <SolidColorBrush x:Key="NodiumGraphMajorGridBrush" Color="#404040" />
    <SolidColorBrush x:Key="NodiumGraphNodeBodyBrush" Color="#1E1E1E" />
    <SolidColorBrush x:Key="NodiumGraphNodeHeaderBrush" Color="#2D2D30" />
    <SolidColorBrush x:Key="NodiumGraphNodeHeaderForegroundBrush" Color="#F5F5F5" />
  </Window.Resources>

  <ng:NodiumGraphCanvas ... />
</Window>
```

### 5. Set the canvas background separately

The background behind the grid is *not* themed through a resource key — it's just the standard Avalonia `Background` property of the canvas, set however you normally set a `Control.Background`. This is intentional: backgrounds are usually unique per screen, whereas the grid and chrome are consistent across your app.

```xml
<ng:NodiumGraphCanvas Background="#F1F5F9" ShowGrid="True" />
```

Or bind it, style it, or gradient-fill it like any other `Control`.

### 6. React to theme variant changes

If your app supports dark / light mode, put the overrides inside `ThemeDictionaries` and Avalonia will swap them automatically when `Application.Current.RequestedThemeVariant` changes.

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
      <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="NodiumGraphGridBrush" Color="#E2E8F0" />
        <SolidColorBrush x:Key="NodiumGraphMajorGridBrush" Color="#94A3B8" />
        <SolidColorBrush x:Key="NodiumGraphNodeBodyBrush" Color="#FFFFFF" />
      </ResourceDictionary>
      <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="NodiumGraphGridBrush" Color="#1E293B" />
        <SolidColorBrush x:Key="NodiumGraphMajorGridBrush" Color="#334155" />
        <SolidColorBrush x:Key="NodiumGraphNodeBodyBrush" Color="#0F172A" />
      </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

No C# code is needed — the canvas re-renders with the new brushes on the next frame after the theme variant changes.

## Full code

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyApp.App">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light">
          <SolidColorBrush x:Key="NodiumGraphGridBrush" Color="#E2E8F0" />
          <SolidColorBrush x:Key="NodiumGraphMajorGridBrush" Color="#94A3B8" />
          <SolidColorBrush x:Key="NodiumGraphOriginXAxisBrush" Color="#EF4444" />
          <SolidColorBrush x:Key="NodiumGraphOriginYAxisBrush" Color="#10B981" />
          <SolidColorBrush x:Key="NodiumGraphMarqueeFillBrush" Color="#2099C1FF" />
          <SolidColorBrush x:Key="NodiumGraphMarqueeBorderBrush" Color="#0EA5E9" />
          <SolidColorBrush x:Key="NodiumGraphConnectionPreviewValidBrush" Color="#10B981" />
          <SolidColorBrush x:Key="NodiumGraphConnectionPreviewInvalidBrush" Color="#EF4444" />
        </ResourceDictionary>
      </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

```csharp
// Optional: reference keys from C# to avoid string literals.
using NodiumGraph;

var gridBrushKey = NodiumGraphResources.GridBrushKey;       // "NodiumGraphGridBrush"
var marqueeKey = NodiumGraphResources.MarqueeFillBrushKey;  // "NodiumGraphMarqueeFillBrush"
```

## Gotchas

- **Use `SolidColorBrush x:Key="..."`, not `DynamicResource`, in your dictionary.** Resources *are* the values; the canvas uses `DynamicResource` internally to look them up. You only need to declare the resource once.
- **Key strings are case-sensitive** and must match exactly — use the constants on `NodiumGraphResources` to avoid typos. Avalonia silently falls back to the default when a key misses, so a typo looks like "the theme didn't apply".
- **The canvas `Background` is a plain `Control.Background`, not a themed key.** If you want the background to participate in theme variants, use `DynamicResource` for its value in AXAML the normal way.
- **Avalonia resources do not inherit across windows opened outside the main application scope.** Resources set on a `Window` only apply to that window's visual tree. Put global overrides in `Application.Resources`.
- **Changing resources at runtime triggers a redraw.** You do not need to call `InvalidateVisual`; Avalonia raises the appropriate dirty notification when a `DynamicResource` target changes.
- **`ConnectionStyle` and `PortStyle` overrides apply *on top* of these brushes.** A per-port `Port.Style.Fill` wins over `NodiumGraphPortBrush`. If something isn't taking effect, check whether a more specific style is winning.

## See also

- [NodiumGraphCanvas control reference](../reference/canvas-control.md)
- [Style ports](style-ports.md)
- [Custom connection style](custom-style.md)
- [Define a custom node DataTemplate](custom-node-template.md)
- [Rendering pipeline reference](../reference/rendering-pipeline.md)
