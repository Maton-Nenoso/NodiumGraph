# Connection API Reference

This page documents the types that describe how a connection looks: the `IConnectionStyle` strategy interface, the built-in `ConnectionStyle`, and the `IEndpointRenderer` contract with its five built-in renderers. All live in `NodiumGraph.Interactions`. See the [strategies reference](strategies.md) for routing and validation, and the [rendering pipeline reference](rendering-pipeline.md) for where these types plug into the draw loop.

## IConnectionStyle

```csharp
public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
    IEndpointRenderer? SourceEndpoint { get; }
    IEndpointRenderer? TargetEndpoint { get; }
}
```

| Member | Meaning |
|---|---|
| `Stroke` | Brush used to stroke the connection, and as the fill brush for filled endpoints. |
| `Thickness` | Stroke thickness in world units. Must be positive. |
| `DashPattern` | Optional `IDashStyle` for dashed lines. `null` means solid. |
| `SourceEndpoint` | Optional decoration at the source end. `null` means no decoration. |
| `TargetEndpoint` | Optional decoration at the target end. `null` means no decoration. |

A `null` endpoint is equivalent to `NoneEndpoint.Instance`: nothing is drawn and no path inset is applied. Source and target endpoints may differ — a diamond source with an arrow target is supported and drawn independently.

## ConnectionStyle (built-in)

```csharp
public ConnectionStyle(
    IBrush? stroke = null,
    double thickness = 2.0,
    IDashStyle? dashPattern = null,
    IEndpointRenderer? sourceEndpoint = null,
    IEndpointRenderer? targetEndpoint = null);
```

`ConnectionStyle` is the default implementation. `stroke` defaults to `Brushes.Gray`, `thickness` to `2.0`, and both endpoints to `null`. The constructor throws `ArgumentOutOfRangeException` on a non-positive thickness. The type is immutable; instances are safe to share. For anything the defaults can't express, implement `IConnectionStyle` yourself.

## IEndpointRenderer contract

```csharp
public interface IEndpointRenderer
{
    double GetInset(double strokeThickness);
    bool IsFilled { get; }
    Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness);
}
```

- **`BuildGeometry(tip, direction, strokeThickness)`** builds the decoration geometry in world coordinates. `tip` is the port center; `direction` is a unit vector pointing outward along the curve tangent at this endpoint. The returned `Geometry` must already be rotated and translated into world space, typically by assigning a `MatrixTransform` to `Geometry.Transform`.
- **`GetInset(strokeThickness)`** returns how many world units the connection stroke should be shortened at this endpoint so it meets the decoration cleanly. Return `0` for no inset. The argument lets stroke-aware shapes account for half the connection's stroke width.
- **`IsFilled`** chooses whether the geometry joins the filled pass or the stroked pass. Filled shapes are drawn with `Stroke` as the fill; open shapes are drawn stroke-only with the connection pen.

**Direction contract.** `direction` must be a non-zero unit vector. Implementations should throw `ArgumentException` on a zero vector — a zero direction is a caller bug (degenerate tangent) and should surface. Built-in renderers are stateless and immutable, so one instance can be shared across any number of styles; `NoneEndpoint` exposes a `Instance` singleton.

## Built-in renderers

| Renderer | Constructor args | `IsFilled` | `GetInset` | Shape |
|---|---|---|---|---|
| `NoneEndpoint.Instance` | — (singleton) | `false` | `0` | nothing (sentinel) |
| `ArrowEndpoint` | `size = 8`, `filled = true` | ctor arg | `size` filled, `size * 0.9` open | triangle |
| `DiamondEndpoint` | `size = 10`, `filled = true` | ctor arg | `size` | rhombus |
| `CircleEndpoint` | `radius = 5`, `filled = true` | ctor arg | `radius * 2` | disc |
| `BarEndpoint` | `width = 2`, `length = 12` | always `false` | `width / 2 + strokeThickness / 2` | perpendicular line |

`ArrowEndpoint`, `DiamondEndpoint`, and `CircleEndpoint` take a `filled` flag; filled means a closed, stroke-colored fill, unfilled means a stroked outline. `BarEndpoint` is stroke-only by design.

## Authoring a custom renderer

Build the canonical geometry at the origin pointing along `+X`, then apply a rotation and translation via `MatrixTransform` to land it at `tip` aligned with `direction`. Close the figure for filled shapes, leave it open for stroke-only. Throw `ArgumentException` on a zero direction.

```csharp
public sealed class PentagonEndpoint : IEndpointRenderer
{
    public PentagonEndpoint(double size = 10, bool filled = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        Size = size;
        IsFilled = filled;
    }

    public double Size { get; }
    public bool IsFilled { get; }

    public double GetInset(double strokeThickness) => Size;

    public Geometry BuildGeometry(Point tip, Vector direction, double strokeThickness)
    {
        if (direction.X == 0 && direction.Y == 0)
            throw new ArgumentException("direction must be non-zero", nameof(direction));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, 0), IsFilled);
            for (int i = 1; i <= 4; i++)
            {
                var theta = Math.PI + i * (2 * Math.PI / 5);
                ctx.LineTo(new Point(Size * Math.Cos(theta), Size * Math.Sin(theta)));
            }
            ctx.EndFigure(IsFilled);
        }

        var rotation = Matrix.CreateRotation(Math.Atan2(direction.Y, direction.X));
        var translation = Matrix.CreateTranslation(tip.X, tip.Y);
        geo.Transform = new MatrixTransform(rotation * translation);
        return geo;
    }
}
```

## Bucketing note

At draw time `ConnectionRenderer` sorts every endpoint into one of two buckets by `IsFilled`: a filled-geometry bucket and an open-geometry bucket. Each bucket is issued as a single `DrawGeometry` call per frame, so the per-frame draw-call count does not scale with the number of decorated connections. That is why `IsFilled` is a plain property rather than something computed per call — the renderer needs a stable bucket choice to batch efficiently.
