---
title: Anchor-based port positioning
tags: [plan, spec]
status: active
created: 2026-05-13
updated: 2026-05-13
---

# Anchor-based port positioning

Implements the `PortAnchor` item from the Port UX 1.0 bundle in [[ROADMAP]]. Today, `Port.Position` is a raw `Point` set at construction; `FixedPortProvider` with `layoutAware: true` performs a one-shot snap-to-boundary in `UpdateLayout` that drifts on resize because the snap uses the port's current position as a direction hint. Anchors replace this with a deterministic `(edge, fraction)` spec that is shape-aware and stable across resizes.

## Goals

- `PortAnchor` (edge + 0..1 fractional offset) is the single, declarative spec for where a port lives on its owner node.
- `Port.Position` is **derived** from `Anchor + Owner.Width/Height + Owner.Shape`. Always boundary-correct, no drift.
- All three built-in shapes (`RectangleShape`, `RoundedRectangleShape`, `EllipseShape`) support the new contract; the shape decides how an edge maps to its boundary (so ellipse ports keep non-cardinal emission normals).
- Node consumers specify anchors at port construction; the derived `Node` class owns its port topology — there is no consumer-facing API to relocate a port after creation.
- `DynamicPortProvider` continues to work; ports created from boundary hits are anchored via `INodeShape.InferAnchor`.

## Non-goals

- **Auto-distribution of fractions** when only an edge is specified. That's the `PortLayout` item (next roadmap entry); this design lays the foundation so it composes additively.
- **Runtime anchor mutation** (`port.Anchor = ...`). The Tier B "Dynamic port reordering along an edge" item will introduce controlled mutation through the provider, not through `Port`.
- **Inset / free-floating ports** (ports that sit inside the node body or float outside the boundary). Out of scope; not a current consumer requirement.
- **Routing algorithm changes.** Routers continue to consume `port.EmissionDirection` (formerly `PortEmissionDirection.Resolve(port)`); behavior is preserved for rectangles, improved for ellipses.

## Design

### Section A — `PortAnchor`, `INodeShape`, `Node` dispatch

#### `PortAnchor`

Immutable value type. The only spec for port position.

```csharp
public enum PortEdge { Left, Top, Right, Bottom }

public readonly record struct PortAnchor(PortEdge Edge, double Fraction)
{
    public double Fraction { get; } = Validate(Fraction);

    private static double Validate(double f)
    {
        if (double.IsNaN(f) || f < 0.0 || f > 1.0)
            throw new ArgumentOutOfRangeException(nameof(Fraction), "Must be in [0, 1].");
        return f;
    }

    public static PortAnchor Left(double f)   => new(PortEdge.Left,   f);
    public static PortAnchor Top(double f)    => new(PortEdge.Top,    f);
    public static PortAnchor Right(double f)  => new(PortEdge.Right,  f);
    public static PortAnchor Bottom(double f) => new(PortEdge.Bottom, f);
}
```

- `record struct` — value equality, hash, immutability, zero allocation, AOT-safe.
- Fraction validated at construction; throws `ArgumentOutOfRangeException`, matching the library's existing validation style.
- Static helpers per ergonomic convention.

#### `INodeShape`

Stateless strategy. Gains three methods; the existing `GetNearestBoundaryPoint` is preserved for `DynamicPortProvider`'s hit detection.

```csharp
public interface INodeShape
{
    Point  GetNearestBoundaryPoint(Point centerRelative, double width, double height);  // existing
    Point  GetEdgePoint(PortEdge edge, double fraction, double width, double height);   // new
    Vector GetEdgeEmissionDirection(PortEdge edge, double fraction);                    // new
    PortAnchor InferAnchor(Point boundaryLocal, double width, double height);           // new
}
```

Coordinate conventions:

- `GetEdgePoint`, `InferAnchor` use **node-local** coordinates (top-left origin), matching `Port.Position`.
- `GetNearestBoundaryPoint` keeps its **center-relative** convention (internal to providers, unchanged).
- `GetEdgeEmissionDirection` returns a unit-ish outward `Vector` (rectangles return exactly unit cardinals; ellipses return outward radial normals).

**Round-trip contract** (the most important invariant): for any `(edge, fraction)` produced by `InferAnchor(p, w, h)`, calling `GetEdgePoint(edge, fraction, w, h)` returns `p` within float epsilon. Each shape implementation must honor this for any on-boundary point.

#### `Node` dispatch

`Node` is the only place that holds dimensions and shape together; it forwards to the shape strategy and exposes the bound API the rest of the library uses.

```csharp
public class Node : INotifyPropertyChanged
{
    public INodeShape Shape { get; set; } = new RectangleShape();
    public double Width  { get; internal set; }
    public double Height { get; internal set; }

    public Point GetEdgePoint(PortEdge edge, double fraction) =>
        Shape.GetEdgePoint(edge, fraction, Width, Height);

    public Vector GetEdgeEmissionDirection(PortEdge edge, double fraction) =>
        Shape.GetEdgeEmissionDirection(edge, fraction);

    public PortAnchor InferAnchor(Point boundaryLocal) =>
        Shape.InferAnchor(boundaryLocal, Width, Height);

    public Point GetNearestBoundaryPoint(Point centerRelative) =>
        Shape.GetNearestBoundaryPoint(centerRelative, Width, Height);
}
```

`Node.Shape` setter must raise `PropertyChanged(nameof(Shape))` — required to invalidate `Port.Position` cache when consumers swap a node's shape at runtime.

### Section B — `Port` wiring and invalidation chain

#### Constructor and properties

```csharp
public class Port : INotifyPropertyChanged
{
    private Point _cachedPosition;
    private Point _cachedAbsolutePosition;
    private bool _positionDirty = true;
    private bool _absolutePositionDirty = true;

    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }
    public PortAnchor Anchor { get; }   // immutable, set in ctor

    public Port(Node owner, string name, PortFlow flow, PortAnchor anchor)
    {
        Owner  = owner ?? throw new ArgumentNullException(nameof(owner));
        Name   = name  ?? throw new ArgumentNullException(nameof(name));
        Flow   = flow;
        Anchor = anchor;
        Owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public Point Position
    {
        get
        {
            if (_positionDirty)
            {
                _cachedPosition = Owner.GetEdgePoint(Anchor.Edge, Anchor.Fraction);
                _positionDirty = false;
            }
            return _cachedPosition;
        }
    }

    public Point AbsolutePosition { /* existing pattern: Owner.X + Position.X, Owner.Y + Position.Y, cached */ }

    public Vector EmissionDirection
        => Owner.GetEdgeEmissionDirection(Anchor.Edge, Anchor.Fraction);
}
```

#### Invalidation chain

| Owner property changed | Invalidate `Position`? | Invalidate `AbsolutePosition`? | Reason |
|---|---|---|---|
| `X`, `Y` | no | yes | Anchor → local position unchanged; world position shifts. |
| `Width`, `Height` | **yes** | yes | `GetEdgePoint(edge, fraction, w, h)` depends on `w`/`h`. |
| `Shape` | **yes** | yes | Different strategy → different boundary geometry. |

```csharp
private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    var name = e.PropertyName;
    if (name is nameof(Node.Width) or nameof(Node.Height) or nameof(Node.Shape))
    {
        _positionDirty = true;
        _absolutePositionDirty = true;
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(AbsolutePosition));
    }
    else if (name is nameof(Node.X) or nameof(Node.Y))
    {
        _absolutePositionDirty = true;
        OnPropertyChanged(nameof(AbsolutePosition));
    }
}
```

`Position` is fully read-only — no `internal set`. The library has exactly one positioning source per port: its `Anchor`.

`Detach()` continues to unsubscribe from `Owner.PropertyChanged`; existing call sites in `FixedPortProvider.RemovePort` / `DynamicPortProvider.CancelResolve` / `NotifyDisconnected` are unchanged.

Edge case: nodes default to `Width = Height = 0` until measured. `Owner.GetEdgePoint(...)` returns `(0, 0)` on uninitialized dimensions — acceptable; `Position` becomes meaningful on first layout.

### Section C — Provider changes

#### `FixedPortProvider`

```csharp
public class FixedPortProvider : IPortProvider     // no longer ILayoutAwarePortProvider
{
    private const double DefaultHitRadius = 20.0;
    private readonly List<Port> _ports = new();
    private readonly double _hitRadiusSq;

    public IReadOnlyList<Port> Ports { get; }
    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;

    public FixedPortProvider(double hitRadius = DefaultHitRadius) { /* unchanged */ }
    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius) { /* unchanged */ }

    public void AddPort(Port port)                          { /* unchanged */ }
    public bool RemovePort(Port port)                       { /* unchanged */ }
    public Port? ResolvePort(Point worldPos, bool preview)  { /* unchanged */ }
    public void CancelResolve() { }
}
```

Removed:
- `bool layoutAware` ctor flag (no longer meaningful).
- `INodeShape _lastShape`, `_lastWidth`, `_lastHeight` private state.
- `UpdateLayout(width, height, shape)` method.
- `LayoutInvalidated` event.
- The boundary-snap loop (its "drift on resize" bug vanishes — `Port.Position` is always on the boundary by construction).

#### `DynamicPortProvider`

Public API unchanged. Internally, ports are constructed with anchors derived via `Node.InferAnchor`:

```csharp
public Port? ResolvePort(Point worldPos, bool preview)
{
    if (_owner.Width <= 0 || _owner.Height <= 0) return null;

    var boundaryWorld = FindNearestBoundaryPoint(worldPos);
    if (boundaryWorld is null) return null;

    // Reuse existing port within threshold — unchanged.
    foreach (var existing in _ports) { /* distance check on AbsolutePosition */ }
    if (preview) return null;

    var boundaryLocal = new Point(boundaryWorld.Value.X - _owner.X, boundaryWorld.Value.Y - _owner.Y);
    var anchor = _owner.InferAnchor(boundaryLocal);

    var port = new Port(_owner, string.Empty, PortFlow.Input, anchor);
    _ports.Add(port);
    _lastCreated = port;
    PortAdded?.Invoke(port);
    return port;
}
```

`AutoPruneOnDisconnect`, `NotifyDisconnected`, `PruneUnconnected` — unchanged.

#### `ILayoutAwarePortProvider` removal

Single implementer (`FixedPortProvider`) no longer needs layout callbacks. Pre-1.0, no consumer implementers exist. The interface, its `UpdateLayout` and `LayoutInvalidated` members, and the canvas's `OnLayoutAwareProviderInvalidated` subscribe/detach wiring in `AttachProvider`/`DetachProvider` are all removed. Resize-driven repaint flows through `Node.PropertyChanged` on `Width`/`Height` (which the canvas already needs for connection invalidation).

### Section D — Routing integration

`PortEmissionDirection` (internal static class) is deleted. Its work collapses to a `Port` property:

```csharp
public Vector EmissionDirection
    => Owner.GetEdgeEmissionDirection(Anchor.Edge, Anchor.Fraction);
```

Router call sites change from `PortEmissionDirection.Resolve(port)` to `port.EmissionDirection`. Affected files identified during the plan phase via grep on `PortEmissionDirection.Resolve`.

Shape implementations of `GetEdgeEmissionDirection`:

- **`RectangleShape`** — cardinal unit vector per edge. Identical output to today's `Resolve` for any on-boundary or near-boundary port.
- **`EllipseShape`** — each `PortEdge` maps to a 90° arc; `fraction` parameterizes position on it. Returns the outward radial normal at that arc point. Realizes the non-cardinal emission case called out in the round-node memory note.
- **`RoundedRectangleShape`** — flat-edge regions return cardinal vectors (matching `RectangleShape`). Corner regions don't appear as emission inputs because `InferAnchor` snaps corner round-off points to the nearest flat edge.

Behavior summary:
- Rectangle nodes — no observable change.
- Ellipse nodes — ports at non-midpoint fractions emit along the smooth radial normal (e.g. `(Right, 0.0)` emits at -45°). Existing ellipse-router tests that asserted cardinal-only behavior, if any, are updated to the new (correct) values.
- Interior/outside-bounds ports — unreachable. The defensive negative-distance code in current `Resolve` is deleted.

### Section E — Breaking changes summary

Per `CLAUDE.md`'s pre-1.0 policy, no shims, no deprecation wrappers.

**`Port`**
- `Port(Node, Point)` constructor — deleted. Replaced by `Port(Node, string name, PortFlow, PortAnchor)`.
- `Position` — `internal set` removed; get-only.
- `Anchor` — new immutable property.
- `EmissionDirection` — new derived property.

**`INodeShape`**
- +3 methods: `GetEdgePoint`, `GetEdgeEmissionDirection`, `InferAnchor`.

**`Node`**
- +4 forwarding wrappers.
- `Shape` setter raises INPC (add if missing).

**`FixedPortProvider`**
- `bool layoutAware` ctor flag removed.
- No longer implements `ILayoutAwarePortProvider`.
- `UpdateLayout`, `LayoutInvalidated` removed.

**`DynamicPortProvider`**
- Public surface unchanged; internal port construction switches to anchors.

**Deleted types**
- `ILayoutAwarePortProvider`
- `PortEmissionDirection`

**Canvas**
- `OnLayoutAwareProviderInvalidated` + the subscribe/detach wiring removed.

## Test strategy

| Area | Action |
|---|---|
| `PortAnchorTests` | **New.** Construction validation (NaN / negative / >1 throws), value equality, hash, static helpers. |
| `RectangleShapeTests` | **Extend.** Cover `GetEdgePoint`, `GetEdgeEmissionDirection`, `InferAnchor`. |
| `EllipseShapeTests` | **Extend.** Same three. Lock non-cardinal emission at non-midpoint fractions. |
| `RoundedRectangleShapeTests` | **Extend.** Same three. Corner-snap behavior for `InferAnchor`. |
| Round-trip property tests | **New per shape.** `GetEdgePoint → InferAnchor` returns the same anchor (float epsilon); `InferAnchor → GetEdgePoint` returns the same boundary point. |
| `PortTests` | **Rewrite.** Anchor-based ctor, `Position` updates on Width/Height/Shape change with INPC, `EmissionDirection` matches `Owner.GetEdgeEmissionDirection`. |
| `FixedPortProviderTests` | **Trim.** Delete `Implements_ILayoutAwarePortProvider`, all `UpdateLayout`/snap tests. |
| `DynamicPortProviderTests` | **Extend.** Created port's anchor round-trips to the hit point. Reuse-threshold unchanged. |
| `BezierRouterTests` / `StepRouterTests` | **Adapt.** Replace any `PortEmissionDirection.Resolve` references with `port.EmissionDirection`. Add at least one ellipse-node emission case to lock non-cardinal behavior. |
| Canvas integration tests | **Adapt.** Anything referencing `ILayoutAwarePortProvider` or `LayoutInvalidated` removed. |
| Sample apps | **Update.** Port construction sites switch to anchor form. Provides realistic-shape exercise. |

## Done criteria

1. All existing tests pass after migration.
2. New `PortAnchorTests` + per-shape round-trip tests pass.
3. New ellipse non-cardinal emission test passes.
4. Sample apps build and run; no visible regression in port placement vs. current `main`.
5. `dotnet build` clean, no new warnings.
6. AOT-compatibility preserved (no reflection introduced).

## Source documents

- [[2026-04-13-port-improvements-prioritized]] — value/difficulty matrix; this design implements the "Anchor-based positioning" entry from the Port UX 1.0 bundle.
- [[2026-04-19-steprouter-port-direction-design]] — established `PortEmissionDirection` as the routing-side emission helper; this design subsumes that helper into `Port.EmissionDirection` and shape dispatch.
- [[ROADMAP]] — overall roadmap; Port UX 1.0 bundle defined there.
