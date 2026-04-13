# Define a Custom Node DataTemplate

## Goal

Control what each node looks like on the canvas — header text, body content, styling, collapse behaviour — using an Avalonia `DataTemplate` keyed on your own `Node` subclass.

## Prerequisites

- You already host `NodiumGraphCanvas`. If not, see [Host the Canvas](host-canvas.md).
- You have a subclass of `Node` for your domain. If not, see [Subclass the model](subclass-model.md).

## Steps

### 1. Understand the two building blocks

NodiumGraph renders nodes through Avalonia's standard `DataTemplate` system. You have two options for the template's visual tree:

- **`NodePresenter`** — a `ContentControl` in `NodiumGraph.Controls` that ships a ready-made node chrome: rounded border, shadow, header bar, collapse toggle. You only define the body content. This is what the Getting Started sample and most real apps use.
- **Anything else** — any Avalonia visual tree you want. NodiumGraph does not require `NodePresenter`. It will faithfully render a raw `Border`, a `Grid`, even an entirely custom control — as long as the root resolves to an Avalonia `Control`.

### 2. Use `NodePresenter` for the common case

`NodePresenter` binds its header text to `Node.Title` automatically (via a `TemplateBinding` in the default `ControlTheme`), and it honours the base `Node.ShowHeader` and `Node.IsCollapsed` properties out of the box. You therefore only need to:

- Bind any of your own subclass properties inside the content slot
- Override the header / border styling properties you care about

```xml
<Window.DataTemplates>
  <DataTemplate DataType="local:MathNode">
    <ng:NodePresenter HeaderBackground="#6366F1"
                      HeaderForeground="White"
                      HeaderFontWeight="SemiBold"
                      HeaderPadding="12,8"
                      CornerRadius="8">
      <StackPanel Spacing="4">
        <TextBlock Text="{Binding Description}"
                   Foreground="#475569"
                   FontSize="12" />
        <TextBlock Text="{Binding Formula}"
                   FontFamily="Consolas"
                   FontSize="11" />
      </StackPanel>
    </ng:NodePresenter>
  </DataTemplate>
</Window.DataTemplates>
```

The `DataContext` inside the template is the `Node` instance itself (in this case a `MathNode`), so `{Binding Description}` reaches your subclass property directly. Because `Node` implements `INotifyPropertyChanged`, changing `Description` at runtime updates the visual without any extra wiring.

### 3. Available `NodePresenter` properties

All of these are standard Avalonia `StyledProperty`s — bindable, stylable, dynamic-resource-friendly. Defaults come from the built-in `ControlTheme` in `src/NodiumGraph/Themes/Generic.axaml`.

| Property | Type | Default |
|---|---|---|
| `HeaderBackground` | `IBrush?` | theme-provided slate |
| `HeaderForeground` | `IBrush?` | theme-provided off-white |
| `HeaderFontSize` | `double` | `12` |
| `HeaderFontWeight` | `FontWeight` | `SemiBold` |
| `HeaderFontFamily` | `FontFamily` | system default |
| `HeaderPadding` | `Thickness` | `10,6` |
| `BoxShadow` | `BoxShadows` | soft 1px card shadow |
| `CollapseToggleForeground` | `IBrush?` | `#94A3B8` |
| `CollapseToggleFontSize` | `double` | `8` |
| `CollapseExpandedGlyph` | `string` | `▲` |
| `CollapseCollapsedGlyph` | `string` | `▼` |
| `CornerRadius` | `CornerRadius` | `8` (from `ContentControl`) |
| `BorderBrush` / `BorderThickness` / `Background` / `Padding` | inherited | theme-provided |

The header text itself is always `{Binding Title}` — it is not exposed as a `NodePresenter` property. Set `Node.Title` on the model to change it.

### 4. Replace `NodePresenter` entirely

If you want a completely different look — no header bar, unusual shape, embedded controls — skip `NodePresenter` and put your own tree in the template:

```xml
<DataTemplate DataType="local:ConstantNode">
  <Border CornerRadius="20"
          Background="#0EA5E9"
          Padding="14,8"
          BoxShadow="0 2 4 0 #30000000">
    <TextBlock Text="{Binding Value, StringFormat={}{0:0.##}}"
               Foreground="White"
               FontWeight="Bold" />
  </Border>
</DataTemplate>
```

The canvas measures the template's root control to determine `Node.Width` and `Node.Height`, so ports declared with `layoutAware: true` on a `FixedPortProvider` automatically land on the correct edge. You do not need to hardcode node dimensions.

### 5. Per-node variants

If different instances of the same CLR type need different visuals (e.g., colour-coded by category), use an Avalonia `DataTrigger` or a `ControlTheme` with class selectors — the same techniques you would use for any other `ContentControl`. You can also add a `Classes` string property on your subclass and bind to it:

```xml
<ng:NodePresenter Classes="{Binding Category}" ...>
```

...then select against it in a `Styles` block:

```xml
<Style Selector="ng|NodePresenter.Math">
  <Setter Property="HeaderBackground" Value="#6366F1" />
</Style>
<Style Selector="ng|NodePresenter.String">
  <Setter Property="HeaderBackground" Value="#10B981" />
</Style>
```

## Full code

The Getting Started sample in `samples/GettingStarted/` contains a complete working `MathNode` template. The AXAML is reproduced above.

## Gotchas

- **The template `DataType` must be the concrete subclass.** `DataType="m:Node"` will match *every* node, including future subclasses. Always target the leaf type (`DataType="local:MathNode"`) when per-type visuals matter.
- **`NodePresenter` is a `ContentControl`, not a panel.** It has exactly one content slot. Wrap multiple children in a `StackPanel` / `Grid` / etc.
- **Templates live in `Window.DataTemplates` (or `UserControl.DataTemplates`, or `Application.DataTemplates`).** Putting them inside the canvas itself does not work — the canvas resolves templates through the normal Avalonia tree, not through its own resources.
- **The default theme binds `Title` and `ShowHeader` via `TemplateBinding`.** If you replace the `NodePresenter` `ControlTheme` wholesale, re-add those bindings or the header will go blank.
- **Changing `Node.Width` / `Node.Height` from your code does nothing visible.** Those setters are `internal set` — the canvas writes them after measuring the template. Size the template, not the model.

## See also

- [Subclass Node / Connection for domain data](subclass-model.md)
- [Theme the canvas (grid, selection, background)](theme-canvas.md)
- [Style ports](style-ports.md)
- [Model reference](../3-reference/model.md)
- [NodiumGraphCanvas control reference](../3-reference/canvas-control.md)
- [Hybrid rendering](../4-explanation/hybrid-rendering.md)
