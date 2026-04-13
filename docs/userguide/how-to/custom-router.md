# Write a Custom IConnectionRouter

## Goal

Replace the default bezier routing with your own path algorithm — orthogonal "elbow" lines, stepped Manhattan routes, custom bezier variants, or anything else you need for your diagram style.

## Prerequisites

- You already host `NodiumGraphCanvas` and have connections rendering. See [Host the Canvas](host-canvas.md).
- You understand that routing runs on every render frame that touches a connection. See the [strategies reference](../reference/strategies.md#iconnectionrouter) and the [rendering pipeline](../reference/rendering-pipeline.md).

## Steps

### 1. The contract

```csharp
public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
    RouteKind RouteKind { get; }
}
```

- `Route` returns the path as an ordered list of world-space `Point`s.
- `RouteKind` tells the renderer how to interpret the list:
  - `RouteKind.Polyline` — straight-line segments through the points, two or more points allowed.
  - `RouteKind.Bezier` — exactly four points `[start, cp1, cp2, end]` describing a cubic bezier.
- The canvas uses the axis-aligned bounding box of the returned points for viewport culling. For beziers, the convex hull of the four points is a conservative bound — no extra work required.
- Returning fewer than two points causes the connection to be skipped that frame. Never return `null`.

### 2. Pick a route kind

Use `Polyline` if you want anything shaped like a sequence of straight segments — orthogonal, diagonal, stepped, zig-zag. Use `Bezier` if you want a single cubic curve per connection. NodiumGraph does not currently support mixing kinds on the same route; a route is either wholly polyline or wholly bezier.

### 3. Example: orthogonal router

A classic "elbow" shape: horizontal from the source, vertical across the midpoint, horizontal into the target.

```csharp
using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class OrthogonalRouter : IConnectionRouter
{
    public RouteKind RouteKind => RouteKind.Polyline;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;
        var midX = (start.X + end.X) / 2;

        return
        [
            start,
            new Point(midX, start.Y),
            new Point(midX, end.Y),
            end,
        ];
    }
}
```

Read each port's `AbsolutePosition` — it already accounts for the owner node's `X` / `Y` and the port's node-local position, so your router never touches the node coordinates directly.

### 4. Example: stepped router with a fixed offset

Sometimes you want the elbow to break a fixed distance from the source rather than at the midpoint — useful when the canvas flows mostly left-to-right and you want to keep the vertical run away from the source node.

```csharp
public sealed class SteppedRouter(double stepOffset = 40.0) : IConnectionRouter
{
    public RouteKind RouteKind => RouteKind.Polyline;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;
        var breakX = start.X + stepOffset;

        return
        [
            start,
            new Point(breakX, start.Y),
            new Point(breakX, end.Y),
            end,
        ];
    }
}
```

### 5. Example: tight bezier variant

If you want the existing curve shape but with a different "tension", write your own bezier router. The default [`BezierRouter`](../reference/strategies.md#built-in-bezierrouter) uses `offset = max(|dx| * 0.4, 30.0)`; tightening the multiplier gives crisper, less floaty curves.

```csharp
public sealed class TightBezierRouter : IConnectionRouter
{
    public RouteKind RouteKind => RouteKind.Bezier;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var dx = end.X - start.X;
        var offset = Math.Max(Math.Abs(dx) * 0.2, 20.0);
        var sign = dx >= 0 ? 1.0 : -1.0;

        return
        [
            start,
            new Point(start.X + offset * sign, start.Y),
            new Point(end.X - offset * sign, end.Y),
            end,
        ];
    }
}
```

### 6. Wire it on the canvas

```csharp
Canvas.ConnectionRouter = new OrthogonalRouter();
```

Or in AXAML, if you prefer:

```xml
<ng:NodiumGraphCanvas ...>
  <ng:NodiumGraphCanvas.ConnectionRouter>
    <local:OrthogonalRouter />
  </ng:NodiumGraphCanvas.ConnectionRouter>
</ng:NodiumGraphCanvas>
```

Assigning `ConnectionRouter` invalidates cached routes and triggers a redraw of every connection on the next frame.

## Full code

```csharp
using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class OrthogonalRouter : IConnectionRouter
{
    public RouteKind RouteKind => RouteKind.Polyline;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;
        var midX = (start.X + end.X) / 2;

        return
        [
            start,
            new Point(midX, start.Y),
            new Point(midX, end.Y),
            end,
        ];
    }
}
```

```csharp
Canvas.ConnectionRouter = new OrthogonalRouter();
```

## Gotchas

- **Routing runs on every frame.** The canvas calls `Route(...)` for every visible connection on every render pass. Keep it allocation-free in the hot path — reuse arrays if you must, but an array literal of four points is fine for 1000+ connections on a modern machine.
- **`RouteKind.Bezier` requires exactly four points.** No more, no less. Returning three or five for a bezier route leaves the connection in an undefined state — validate at the type level, not in comments.
- **`RouteKind` must be stable per-instance.** Don't flip between `Polyline` and `Bezier` depending on which route you're computing; the canvas caches pens and path geometry assuming the kind does not change. If you need both, use two router instances or partition by connection kind using a router that selects internally but always reports the same `RouteKind`.
- **Use `AbsolutePosition`, not `Position`.** `Port.Position` is node-local; `Port.AbsolutePosition` has the owner node's origin added. Routing against `Position` will put every connection at the top-left of the canvas.
- **The AABB of returned points must bound the rendered curve.** For beziers this is automatic — the curve stays inside the convex hull of the control points. For polylines it's also automatic — straight segments never leave their endpoints. If you ever return bezier control points wildly outside the curve (don't), viewport culling will pop connections in and out near the viewport edge.
- **Routers are singleton-like by assignment, not by contract.** The canvas keeps one `ConnectionRouter` at a time. There's no per-connection router override built into the model — if you want one, keep the canvas router as a dispatcher that picks a delegate based on the connection's type or tags.
- **`Port.AbsolutePosition` is cached and invalidated when its owner node moves.** You don't need to worry about stale coordinates inside `Route(...)`; by the time you're called, positions are current.

## See also

- [Strategy interfaces reference](../reference/strategies.md#iconnectionrouter)
- [Rendering pipeline reference](../reference/rendering-pipeline.md)
- [NodiumGraphCanvas control reference](../reference/canvas-control.md)
- [Hybrid rendering](../explanation/hybrid-rendering.md)
- [Custom connection style](custom-style.md)
