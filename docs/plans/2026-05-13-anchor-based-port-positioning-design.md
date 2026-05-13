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
    public PortEdge Edge { get; }     = ValidateEdge(Edge);
    public double   Fraction { get; } = ValidateFraction(Fraction);

    private static PortEdge ValidateEdge(PortEdge edge) => edge switch
    {
        PortEdge.Left or PortEdge.Top or PortEdge.Right or PortEdge.Bottom => edge,
        _ => throw new ArgumentOutOfRangeException(nameof(Edge), $"Invalid PortEdge value: {(int)edge}."),
    };

    private static double ValidateFraction(double f)
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
- Both `Edge` and `Fraction` validated at construction; throws `ArgumentOutOfRangeException`. Edge validation prevents `(PortEdge)999` from leaking into shape `switch` dispatch at render/router time.
- Static helpers per ergonomic convention.

#### `INodeShape`

Stateless strategy. Gains three methods; the existing `GetNearestBoundaryPoint` is preserved for `DynamicPortProvider`'s hit detection.

```csharp
public interface INodeShape
{
    Point  GetNearestBoundaryPoint(Point centerRelative, double width, double height);  // existing
    Point  GetEdgePoint(PortAnchor anchor, double width, double height);                // new
    Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height);        // new
    PortAnchor InferAnchor(Point boundaryLocal, double width, double height);           // new
}
```

The three new methods accept `PortAnchor` rather than raw `(edge, fraction)` pairs. `PortAnchor`'s constructor validates both `Edge` and `Fraction`; threading the validated value type through the shape API closes the bypass where `shape.GetEdgePoint((PortEdge)999, double.NaN, ...)` would otherwise reach the `switch` dispatch with garbage. Width/height stay parameterized because the shape is dimensionless (Section A rationale).

Coordinate conventions:

- `GetEdgePoint`, `InferAnchor` use **node-local** coordinates (top-left origin), matching `Port.Position`.
- `GetNearestBoundaryPoint` keeps its **center-relative** convention (internal to providers, unchanged).
- `GetEdgeOutwardNormal` returns a unit outward `Vector`. The method takes `(width, height)` because non-square boundary geometry produces aspect-dependent normals — for an ellipse `x²/a² + y²/b² = 1` with `a = w/2`, `b = h/2`, the outward normal at the parameterized point is **not** a unit-circle direction.

**Boundary parameterization — full coverage per shape.** Each shape's four `PortEdge` values partition its boundary; the union of the four edges' addressable points is the entire boundary at the current `(w, h)`. Per-shape rules:

- `RectangleShape` — each edge runs along its corresponding side. Trivial partition.
- `EllipseShape` — each `PortEdge` maps to a 90° quadrant arc in Avalonia screen coordinates (`+x` right, `+y` down). Parameterized clockwise; `Fraction = 0` is the start of the clockwise traversal, `Fraction = 1` is its end. With center at `(a, b)` and `a = w/2`, `b = h/2`, the point at parameter `θ` is `(a + a·cosθ, b + b·sinθ)`. Per-edge angle range:

  | Edge | θ at `Fraction = 0` | θ at `Fraction = 1` | Formula |
  |---|---|---|---|
  | `Top`    | `-3π/4` (top-left corner-midpoint)     | `-π/4` (top-right corner-midpoint)     | `θ = -3π/4 + Fraction · π/2` |
  | `Right`  | `-π/4` (top-right corner-midpoint)     | `π/4` (bottom-right corner-midpoint)   | `θ = -π/4  + Fraction · π/2` |
  | `Bottom` | `π/4` (bottom-right corner-midpoint)   | `3π/4` (bottom-left corner-midpoint)   | `θ = π/4   + Fraction · π/2` |
  | `Left`   | `3π/4` (bottom-left corner-midpoint)   | `5π/4` (top-left corner-midpoint, = -3π/4 mod 2π) | `θ = 3π/4  + Fraction · π/2` |

  No wrap inside any single edge's range. Shared endpoints between adjacent edges land at the four 45° / 135° / etc. corner-midpoints and are governed by the canonical rule below.

- `RoundedRectangleShape` — each `PortEdge` covers half of each adjacent corner arc plus the flat segment between them. The corner arc between `Top` and `Right` is split at its midpoint (45° from the corner center); the first half belongs to `Top`, the second to `Right`. Edge fraction is parameterized by arc length over the combined region. With effective radius `rEff = min(r, w/2, h/2)`:

  | Edge | `edgeLength` | Flat-segment length |
  |---|---|---|
  | `Top`, `Bottom` | `w` | `max(0, w − 2·rEff)` |
  | `Left`, `Right` | `h` | `max(0, h − 2·rEff)` |

  Each half-arc contributes `π·rEff / 4` (a quarter-circle is `π·rEff / 2`; half of that), so per-edge total length is `flatSegment + π·rEff / 2`. Continuous and monotonic from corner-midpoint to corner-midpoint.

  **Capsule / degenerate cases (per-edge, not global).** When `rEff` equals or exceeds half a particular dimension, the flat segment **on edges parallel to that dimension's perpendicular** collapses to 0 — for a horizontal capsule (`w > h`, `r = h/2`), `Left` and `Right` have zero flat (entire edge is two half-arcs joined at `Fraction = 0.5`) while `Top` and `Bottom` still have a `w − h` flat region. For a square node with `r ≥ w/2 = h/2`, all four edges have zero flat. Parameterization stays well-defined in every case and the full boundary remains covered.

**Canonical anchor at shared endpoints.** Adjacent edges share their boundary endpoint: `Top(1)` and `Right(0)` address the same top-right point; `Left(1)` and `Top(0)` address the top-left point; ellipse quadrants share the 45° / 135° / etc. points; rounded-rectangle edges meet at the corner-arc midpoints. To make `InferAnchor` deterministic, walk the boundary clockwise (`Top → Right → Bottom → Left → Top`) — **each edge owns its `Fraction = 0` endpoint and disclaims its `Fraction = 1` endpoint to the next edge**. Canonical anchors at shared corners:

- Top-left corner → `Top(0)` is canonical; `Left(1)` is not.
- Top-right corner → `Right(0)` is canonical; `Top(1)` is not.
- Bottom-right corner → `Bottom(0)` is canonical; `Right(1)` is not.
- Bottom-left corner → `Left(0)` is canonical; `Bottom(1)` is not.

`InferAnchor` always returns a canonical anchor.

**Round-trip contract** — two directions:

1. **Anchor → point → anchor.** For any well-formed `PortAnchor a`, `InferAnchor(GetEdgePoint(a, w, h), w, h)` returns `a` exactly when `a` is canonical, and returns the canonical anchor for the same boundary point when `a` is not (i.e. when `a` is a `Fraction = 1` anchor on an edge whose endpoint is shared with the next edge). In both cases the *boundary point* is preserved exactly.

2. **Boundary point → anchor → boundary point.** For any point `p` lying on the boundary of the shape at `(w, h)`, `GetEdgePoint(InferAnchor(p, w, h), w, h) == p` within float epsilon. This direction always holds unconditionally and is what makes `DynamicPortProvider` predictable: a port created at a boundary hit point lands exactly where the user clicked, including on rounded corners.

#### `Node` dispatch

`Node` is the only place that holds dimensions and shape together; it forwards to the shape strategy and exposes the bound API the rest of the library uses.

```csharp
public class Node : INotifyPropertyChanged
{
    public INodeShape Shape { get; set; } = new RectangleShape();
    public double Width  { get; internal set; }
    public double Height { get; internal set; }

    public Point GetEdgePoint(PortAnchor anchor) =>
        Shape.GetEdgePoint(anchor, Width, Height);

    public Vector GetEdgeOutwardNormal(PortAnchor anchor) =>
        Shape.GetEdgeOutwardNormal(anchor, Width, Height);

    public PortAnchor InferAnchor(Point boundaryLocal) =>
        Shape.InferAnchor(boundaryLocal, Width, Height);

    public Point GetNearestBoundaryPoint(Point centerRelative) =>
        Shape.GetNearestBoundaryPoint(centerRelative, Width, Height);
}
```

`Node.Shape` setter must:
- Reject `null` via `ArgumentNullException.ThrowIfNull(value)` — all forwarding methods dereference `Shape`; a null assignment would NRE on next read.
- Raise `PropertyChanged(nameof(Shape))` after assignment — required to invalidate `Port.Position` cache when consumers swap a node's shape at runtime.

**Zero-dimension behavior — contract for all three new shape methods.** Nodes default to `Width = Height = 0` until first measured; `Port.Position` and `Port.EmissionDirection` can be read in that interval. Each shape method has a defined behavior at zero dimensions:

| Method | At `width <= 0` or `height <= 0` |
|---|---|
| `GetEdgePoint(anchor, w, h)`         | Returns `(0, 0)` (natural degeneration of all three shapes' formulas). |
| `GetEdgeOutwardNormal(anchor, w, h)` | Returns the **cardinal unit vector for `anchor.Edge`** (`Left → (-1, 0)`, etc.). Aspect-aware formulas degenerate at zero size; cardinal is a stable fallback consistent with the edge identity and what routers expect. |
| `InferAnchor(boundaryLocal, w, h)`   | Throws `InvalidOperationException` — there is no meaningful inverse at zero size. Callers (only `DynamicPortProvider`) already guard `_owner.Width <= 0 || _owner.Height <= 0` and return null; the throw is a safety net, not a normal control-flow path. |

Implementers must honor this contract on every shape; tests assert it per shape.

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
                _cachedPosition = Owner.GetEdgePoint(Anchor);
                _positionDirty = false;
            }
            return _cachedPosition;
        }
    }

    public Point AbsolutePosition { /* existing pattern: Owner.X + Position.X, Owner.Y + Position.Y, cached */ }

    public Vector EmissionDirection
        => Owner.GetEdgeOutwardNormal(Anchor);
}
```

The consumer-facing name on `Port` stays `EmissionDirection` (router-side vocabulary). It forwards to `Node.GetEdgeOutwardNormal`, which is the geometric primitive.

#### Invalidation chain

| Owner property changed | Invalidate `Position`? | Invalidate `AbsolutePosition`? | Re-fire `EmissionDirection`? | Reason |
|---|---|---|---|---|
| `X`, `Y` | no | yes | no | Anchor → local position unchanged; world position shifts; emission is local-geometry-only. |
| `Width`, `Height` | **yes** | yes | **yes** | `GetEdgePoint` and `GetEdgeOutwardNormal` both depend on `w`/`h` (aspect-dependent for ellipse / rounded-rect arc regions). |
| `Shape` | **yes** | yes | **yes** | Different strategy → different boundary geometry and different outward normals. |

`EmissionDirection` is not cached (it has no dirty flag); the INPC notification tells listeners the *property value* has changed even though the next read recomputes lazily.

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
        OnPropertyChanged(nameof(EmissionDirection));
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

Edge case: nodes default to `Width = Height = 0` until measured. Per the zero-dimension contract in Section A: `Position` returns `(0, 0)` and `EmissionDirection` returns the edge's cardinal unit vector. Both are stable and safe to read before first layout; values become geometrically meaningful once the node is measured.

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

Single implementer (`FixedPortProvider`) no longer needs layout callbacks. Pre-1.0, no consumer implementers exist. The interface, its `UpdateLayout` and `LayoutInvalidated` members, and the canvas's `OnLayoutAwareProviderInvalidated` subscribe/detach wiring in `AttachProvider`/`DetachProvider` are all removed.

#### Canvas invalidation chain — resize and shape swap

`LayoutInvalidated` is what handles resize/shape-change repaint in current code. Removing it without replacing the path leaves stale visuals. The current `OnNodePropertyChanged` handler (at `NodiumGraphCanvas.cs:1912`) handles `X`, `Y`, `IsSelected`, `ShowHeader`, `IsCollapsed`, `IsCollapsible`, `PortProvider`, `Style` — **no `Width`, `Height`, or `Shape`**. Three cases must be added.

End-to-end chain for a resize or shape swap:

1. `Node.Width` / `Node.Height` / `Node.Shape` setter fires `PropertyChanged` for the changed property.
2. Each `Port` of that node receives it (via its existing `Owner.PropertyChanged` subscription, extended per Section B), invalidates `_positionDirty` + `_absolutePositionDirty`, and raises `PropertyChanged(Position)` + `PropertyChanged(AbsolutePosition)`.
3. Canvas's `OnPortPropertyChanged` already invalidates per-node adornments for `AbsolutePosition` (line 1898) — but it **does not** invalidate connection geometry today. The connection-geometry cache `_connectionGeometryCache` (which also backs `ConnectionHitTester.HitTest`) becomes stale unless we add the invalidation.
4. Canvas's `OnNodePropertyChanged` gains a new case **and** calls `InvalidateConnectionGeometryForNode` explicitly — single call site, fires once per resize event regardless of port count (cleaner than per-port invalidation on every `AbsolutePosition` change):

```csharp
else if (e.PropertyName is nameof(Node.Width) or nameof(Node.Height) or nameof(Node.Shape))
{
    if (sender is Node node)
    {
        InvalidateConnectionGeometryForNode(node);  // drops cached geometry for every connection touching node — also backs ConnectionHitTester
        InvalidateNodeAdornments(node);             // node's NodeAdornmentLayer (existing helper)
        InvalidateVisual();                          // grid/connections/minimap repaint
    }
}
```

5. `NodeAdornmentLayer` re-measures + redraws ports at their new `Position` values on the next render pass; connection geometry is recomputed lazily from fresh `Port.AbsolutePosition` values on the next paint.

Notes:
- `Node.Width` / `Node.Height` are `internal set` — there is no public consumer-resize API. The cases catch the library's own measurement/arrange output (e.g. `NodeContainer` writing back measured dimensions) and any internal/test write. Both flow through the same INPC notification.
- `Shape` swap is a public consumer action (settable property); covered by the same case.
- No new event types or interfaces; the chain rides existing `INotifyPropertyChanged` plumbing and the existing `InvalidateConnectionGeometryForNode` / `InvalidateNodeAdornments` helpers.
- `ConnectionHitTester` consumes `_connectionGeometryCache` directly — there is no separate hit-tester cache, so the single `InvalidateConnectionGeometryForNode` call covers hit-testing too.

### Section D — Routing integration

`PortEmissionDirection` (internal static class) is deleted. Its work collapses to a `Port` property:

```csharp
public Vector EmissionDirection
    => Owner.GetEdgeOutwardNormal(Anchor);
```

Router call sites change from `PortEmissionDirection.Resolve(port)` to `port.EmissionDirection`. Affected files identified during the plan phase via grep on `PortEmissionDirection.Resolve`.

Shape implementations of `GetEdgeOutwardNormal`:

- **`RectangleShape`** — cardinal unit vector per edge, dimensions ignored. Identical output to today's `Resolve` for any on-boundary or near-boundary port.
- **`EllipseShape`** — at the parameterized arc point `(a·cosθ + a, b·sinθ + b)` with `a = w/2`, `b = h/2`, the outward unit normal is `(b·cosθ, a·sinθ)` normalized. **Aspect-ratio aware** — a 200×100 ellipse and a circle return different normals at the same `(edge, fraction)`.
- **`RoundedRectangleShape`** — piecewise per the full-coverage parameterization in Section A:
  - **Flat-segment region** of the edge → cardinal unit vector (same as `RectangleShape`).
  - **Corner-arc region** (the half-arc portion of an edge) → outward radial normal from the corner's arc center to the parameterized point on the arc. This is the right geometric answer for connection emission off rounded corners; a connection from a port near a rounded corner heads outward at the corner's tangent direction, not the adjacent edge's cardinal.

Behavior summary:
- Rectangle nodes — no observable change.
- Ellipse nodes — ports at non-midpoint fractions emit along the aspect-correct radial normal. Existing ellipse-router tests that asserted cardinal-only behavior, if any, are updated to the new (correct) values.
- Rounded-rectangle nodes — flat-region ports emit cardinally (unchanged); corner-arc ports emit along the arc normal. Corner-arc ports are reachable both via `DynamicPortProvider` boundary hits *and* via consumer-supplied fixed anchors that land in a corner region under the new parameterization (e.g. `PortAnchor.Top(0.0)` on a `RoundedRectangleShape` lands at the corner-midpoint of the top-left arc).
- Interior/outside-bounds ports — unreachable. The defensive negative-distance code in current `Resolve` is deleted.

### Section E — Breaking changes summary

Per `CLAUDE.md`'s pre-1.0 policy, no shims, no deprecation wrappers.

**`Port`**
- `Port(Node, string name, PortFlow, Point position)` constructor — deleted.
- `Port(Node, Point)` convenience constructor — deleted.
- Both replaced by `Port(Node, string name, PortFlow, PortAnchor anchor)`.
- `Position` — `internal set` removed; get-only.
- `Anchor` — new immutable property.
- `EmissionDirection` — new derived property.

**`INodeShape`**
- +3 methods: `GetEdgePoint`, `GetEdgeOutwardNormal`, `InferAnchor`.

**`Node`**
- +4 forwarding wrappers.
- `Shape` setter raises INPC (add if missing) **and** rejects `null` via `ArgumentNullException.ThrowIfNull(value)`.

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
| `PortAnchorTests` | **New.** Fraction validation (NaN / negative / >1 throws), `Edge` validation (`(PortEdge)999` and other out-of-range int casts throw `ArgumentOutOfRangeException`), value equality, hash, static helpers. |
| `NodeTests` — `Shape` property | **New / extend.** Setter rejects `null` via `ArgumentNullException`; valid assignment raises `PropertyChanged(nameof(Shape))`. |
| Zero-dimension contract | **New per shape.** `GetEdgePoint` returns `(0, 0)`; `GetEdgeOutwardNormal` returns the cardinal unit vector for the edge; `InferAnchor` throws `InvalidOperationException`. Cases for `width = 0`, `height = 0`, and both zero. |
| `RectangleShapeTests` | **Extend.** Cover `GetEdgePoint`, `GetEdgeOutwardNormal`, `InferAnchor`. |
| `EllipseShapeTests` | **Extend.** Same three. Lock non-cardinal emission at non-midpoint fractions. Endpoint table from Section A: each edge's `Fraction = 0`/`1` lands at the documented angle (top-left corner-midpoint at `θ = -3π/4` for `Top(0)`, etc.); shared endpoints `Top(1) == Right(0)` produce the same boundary point. |
| `RoundedRectangleShapeTests` | **Extend.** Same three. Corner-arc round-trip: a boundary point on a rounded corner round-trips exactly via `InferAnchor → GetEdgePoint`; outward normal on a corner-arc anchor is the radial vector from the corner center. **Per-edge capsule cases**: horizontal capsule (`w > h`, `r = h/2`) — `Left`/`Right` flat = 0 (two half-arcs joined at `Fraction = 0.5`); `Top`/`Bottom` still have `w − h` flat. Square + large radius (`r ≥ w/2`) — all four edges have zero flat. Boundary fully covered in every case. |
| Round-trip property tests | **New per shape.** Three cases: (a) for canonical anchors (Fraction ∈ (0, 1) or canonical corner per Section A), `GetEdgePoint → InferAnchor` returns the same anchor within float epsilon; (b) for non-canonical shared-endpoint anchors (e.g. `Top(1)` at a corner that canonicalizes to the next edge's `Fraction = 0`), `GetEdgePoint → InferAnchor` returns the canonical equivalent — *same boundary point*, possibly different `(Edge, Fraction)`; (c) for any boundary point, `InferAnchor → GetEdgePoint` returns the same point unconditionally. |
| `PortTests` | **Rewrite.** Anchor-based ctor, `Position` updates on Width/Height/Shape change with INPC, `EmissionDirection` matches `Owner.GetEdgeOutwardNormal` and fires INPC on Width/Height/Shape change (not on X/Y change). |
| `FixedPortProviderTests` | **Trim.** Delete `Implements_ILayoutAwarePortProvider`, all `UpdateLayout`/snap tests. |
| `DynamicPortProviderTests` | **Extend.** Created port's anchor round-trips to the hit point. Reuse-threshold unchanged. |
| `BezierRouterTests` / `StepRouterTests` | **Adapt.** Replace any `PortEmissionDirection.Resolve` references with `port.EmissionDirection`. Add at least one ellipse-node emission case to lock non-cardinal aspect-aware behavior. |
| Canvas integration tests | **Adapt.** Anything referencing `ILayoutAwarePortProvider` or `LayoutInvalidated` removed. Add coverage for `Width`/`Height`/`Shape` → port `Position` invalidation → connection geometry invalidation → `InvalidateVisual` (the chain that replaces `LayoutInvalidated`). |
| Sample apps | **Update.** Port construction sites switch to anchor form. Provides realistic-shape exercise. |
| User guide pages | **Update.** Remove `layoutAware`, `ILayoutAwarePortProvider`, `PortEmissionDirection` references. Switch port-construction examples and the model-reference signatures to anchor form in: `1-tutorial/getting-started.md`, `2-how-to/custom-port-provider.md`, `2-how-to/custom-node-template.md`, `2-how-to/style-ports.md`, `3-reference/strategies.md`, `3-reference/model.md` (the `Port` constructor block at lines 100–101 currently documents the deleted point-based constructors). |
| All existing test suites using point-based port construction | **Migrate (broad-touch).** 22 files use `new Port(...)` with a `Point` today: `GraphTests`, `ConnectionTests`, `ConnectionRendererTests`, `ConnectionHitTesterTests`, `BezierRouterTests`, `StraightRouterTests`, `StepRouterTests`, `PortStyleTests`, `DefaultConnectionValidatorTests`, `ISelectionHandlerTests`, all `NodiumGraphCanvas*Tests`, plus the `PortTests` / `FixedPortProviderTests` / `DynamicPortProviderTests` rows above. Replace point-based construction with anchor-based — introduce a small test helper (e.g. `TestNodes.WithPorts(...)`) to keep the migration mechanical and reduce per-test churn. |

## Done criteria

1. All existing tests pass after migration.
2. New `PortAnchorTests` + per-shape round-trip tests pass on each of `RectangleShape`, `EllipseShape`, `RoundedRectangleShape`: (a) canonical anchors round-trip exactly via `GetEdgePoint → InferAnchor`; (b) non-canonical `Fraction = 1` shared-endpoint anchors round-trip to their canonical equivalent (same boundary point); (c) `InferAnchor → GetEdgePoint` preserves any boundary point unconditionally.
3. New ellipse aspect-aware outward-normal test passes (200×100 ellipse and 100×100 circle return different normals at `(Right, 0.0)`).
4. New canvas test passes: `Node.Width`/`Height`/`Shape` change → connection geometry invalidates → ports reposition → canvas visual invalidates.
5. Sample apps build and run; no visible regression in port placement vs. current `main`.
6. User guide pages listed above are updated (including `3-reference/model.md`'s `Port` constructor block); no remaining references to `layoutAware`, `ILayoutAwarePortProvider`, or `PortEmissionDirection` anywhere under `docs/userguide/`.
7. `dotnet build` clean, no new warnings.
8. AOT-compatibility preserved (no reflection introduced).

## Source documents

- [[2026-04-13-port-improvements-prioritized]] — value/difficulty matrix; this design implements the "Anchor-based positioning" entry from the Port UX 1.0 bundle.
- [[2026-04-19-steprouter-port-direction-design]] — established `PortEmissionDirection` as the routing-side emission helper; this design subsumes that helper into `Port.EmissionDirection` and shape dispatch.
- [[ROADMAP]] — overall roadmap; Port UX 1.0 bundle defined there.
