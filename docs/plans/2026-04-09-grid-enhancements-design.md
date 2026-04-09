# Grid Enhancements Design

**Date:** 2026-04-09

Five enhancements to the NodiumGraph grid system, all implemented in `GridRenderer` and exposed via `NodiumGraphCanvas` styled properties.

## 1. GridStyle.None

Add `None` to the `GridStyle` enum. `GridRenderer.Render` returns early. Snap-to-grid remains functional (snap logic is in the pointer handler, independent of rendering).

## 2. Configurable Major Line Interval

`MajorGridInterval` StyledProperty on canvas (`int`, default `5`, min `2`). Replaces the hardcoded constant in `GridRenderer`. Passed through to `Render`.

## 3. Origin Axes

`ShowOriginAxes` bool StyledProperty (default `true`). Two theme resources for per-axis color:

- `NodiumGraphOriginXAxisBrush` (default: semi-transparent red `#60E05050`)
- `NodiumGraphOriginYAxisBrush` (default: semi-transparent green `#6050B050`)

`GridRenderer.RenderOriginAxes` draws horizontal (X) and vertical (Y) lines through world (0,0), full canvas extent, 1.5px pen. Renders regardless of `GridStyle` (including `None`) when `ShowOriginAxes` is true.

## 4. Grid Opacity Fade

Automatic, no new properties. `GridRenderer` computes a fade factor from zoom:

- `zoom >= 0.3`: full opacity
- `0.1 < zoom < 0.3`: linear fade
- `zoom <= 0.1`: grid hidden

Applied via `DrawingContext.PushOpacity` / `PopOpacity` to preserve consumer brush colors.

## 5. Adaptive Grid Density

Compute effective grid spacing from screen-space pixel density:

- Target: 15-60px between grid lines/dots on screen
- `effectiveSize = GridSize`; double while `effectiveSize * zoom < 15`; halve (floor at `GridSize`) while `effectiveSize * zoom > 60`
- Major lines still reference base `GridSize * MajorGridInterval`, not effective size

## Sample App Sidebar

- Add `None` to grid style ComboBox
- `MajorGridInterval` slider (2-20, snap to 1)
- `ShowOriginAxes` toggle

No controls for fade or adaptive density (automatic).

## Theme Resources (new)

| Key | Default |
|-----|---------|
| `NodiumGraphOriginXAxisBrush` | `#60E05050` |
| `NodiumGraphOriginYAxisBrush` | `#6050B050` |
