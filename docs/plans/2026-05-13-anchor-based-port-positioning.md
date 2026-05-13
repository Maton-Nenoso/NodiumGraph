---
title: Anchor-based port positioning — implementation plan
tags: [plan]
status: active
created: 2026-05-13
updated: 2026-05-13
---

# Anchor-based port positioning — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers-extended-cc:subagent-driven-development` (recommended) or `superpowers-extended-cc:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace raw `Point`-based `Port.Position` with `PortAnchor` (edge + fraction) as the declarative, shape-aware port-placement spec.

**Architecture:** `PortAnchor` is an immutable validated value type. `INodeShape` gains three methods (`GetEdgePoint`, `GetEdgeOutwardNormal`, `InferAnchor`) that all take `PortAnchor`. `Node` exposes forwarding wrappers and gains a non-null `Shape` invariant. `Port.Position` becomes derived from `Anchor + Owner.Width/Height/Shape`. `FixedPortProvider`'s `layoutAware` flag, `ILayoutAwarePortProvider`, and the standalone `PortEmissionDirection` static helper all collapse — emission becomes `Port.EmissionDirection`. Canvas invalidation rides existing INPC plumbing with one new property-changed case.

**Tech Stack:** C# 10, .NET 10, Avalonia 12, xUnit v3, Avalonia.Headless.XUnit.

**Spec:** [[2026-05-13-anchor-based-port-positioning-design]] (rev 11, commit `e0a13c5`).

---

## File map

**New files:**
- `src/NodiumGraph/Model/PortEdge.cs` — enum.
- `src/NodiumGraph/Model/PortAnchor.cs` — record struct with Edge + Fraction validation.
- `tests/NodiumGraph.Tests/PortAnchorTests.cs` — validation, equality, static helpers.
- `tests/NodiumGraph.Tests/Helpers/TestNodes.cs` — shared test helper that constructs ports at a given on-boundary point (today wraps the old `Point` ctor; updated in Task 7 to compute the anchor via `Node.InferAnchor`).

**Modified files:**
- `src/NodiumGraph/Model/INodeShape.cs` — +3 methods with default-throwing implementations (overridden by every built-in shape).
- `src/NodiumGraph/Model/RectangleShape.cs` — implement 3 new methods.
- `src/NodiumGraph/Model/EllipseShape.cs` — implement 3 new methods, aspect-aware outward normal.
- `src/NodiumGraph/Model/RoundedRectangleShape.cs` — implement 3 new methods, per-edge flat-length math, capsule cases.
- `src/NodiumGraph/Model/Node.cs` — +4 forwarding wrappers; `Shape` setter null-guard and INPC.
- `src/NodiumGraph/Model/Port.cs` — anchor-based constructor; `Position` read-only/derived/cached; new `EmissionDirection` property; invalidation chain extended to `Width`/`Height`/`Shape`.
- `src/NodiumGraph/Model/FixedPortProvider.cs` — drop `layoutAware` flag, `UpdateLayout`, `LayoutInvalidated`, `_lastShape`/`_lastWidth`/`_lastHeight`; no longer implements `ILayoutAwarePortProvider`.
- `src/NodiumGraph/Model/DynamicPortProvider.cs` — construct anchored ports via `Node.InferAnchor`.
- `src/NodiumGraph/Controls/NodiumGraphCanvas.cs` — delete `OnLayoutAwareProviderInvalidated` and its wiring in `AttachProvider`/`DetachProvider`; add `Width`/`Height`/`Shape` case in `OnNodePropertyChanged` that calls `InvalidateConnectionGeometryForNode` + `InvalidateNodeAdornments` + `InvalidateVisual`.
- `src/NodiumGraph/Interactions/BezierRouter.cs`, `StepRouter.cs`, `StraightRouter.cs`, and any other call site — replace `PortEmissionDirection.Resolve(port)` with `port.EmissionDirection`.
- 22 test files using point-based `new Port(...)` — migrate via `TestNodes` helper. List: `GraphTests`, `ConnectionTests`, `ConnectionRendererTests`, `ConnectionHitTesterTests`, `BezierRouterTests`, `StraightRouterTests`, `StepRouterTests`, `PortStyleTests`, `DefaultConnectionValidatorTests`, `ISelectionHandlerTests`, all `NodiumGraphCanvas*Tests` (Selection, Keyboard, ConnectionCache, Render, Cutting, ApiWiring, ConnectionDraw, GraphBinding, ConnectionDefaults), `PortTests`, `FixedPortProviderTests`, `DynamicPortProviderTests`.
- `samples/GettingStarted/`, `samples/NodiumGraph.Sample/` — port construction sites switch to anchor form.
- User guide pages (see Task 11 for the enumerated set).

**Deleted files:**
- `src/NodiumGraph/Model/ILayoutAwarePortProvider.cs`.
- `src/NodiumGraph/Interactions/PortEmissionDirection.cs`.

---

## Task 1: `PortEdge` + `PortAnchor`

**Goal:** Land the foundational types with validation. No downstream impact yet.

**Files:**
- Create: `src/NodiumGraph/Model/PortEdge.cs`
- Create: `src/NodiumGraph/Model/PortAnchor.cs`
- Create: `tests/NodiumGraph.Tests/PortAnchorTests.cs`

**Acceptance Criteria:**
- [ ] `PortEdge { Left, Top, Right, Bottom }` exists.
- [ ] `PortAnchor(PortEdge, double)` constructor throws `ArgumentOutOfRangeException` for invalid `Edge` (e.g. `(PortEdge)999`).
- [ ] `PortAnchor` constructor throws `ArgumentOutOfRangeException` for `Fraction` < 0, > 1, or `NaN`.
- [ ] `PortAnchor` static helpers `Left(f)`, `Top(f)`, `Right(f)`, `Bottom(f)` produce the right edge.
- [ ] Record-struct value equality works.

**Verify:** `dotnet test --filter "FullyQualifiedName~PortAnchorTests"` → all tests pass; full build clean.

**Steps:**

- [ ] **Step 1: Write `PortAnchorTests` first**

```csharp
// tests/NodiumGraph.Tests/PortAnchorTests.cs
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortAnchorTests
{
    [Fact]
    public void Throws_on_invalid_PortEdge_cast()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAnchor((PortEdge)999, 0.5));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Throws_on_invalid_Fraction(double f)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAnchor(PortEdge.Top, f));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Accepts_Fraction_in_unit_interval(double f)
    {
        var a = new PortAnchor(PortEdge.Top, f);
        Assert.Equal(f, a.Fraction);
        Assert.Equal(PortEdge.Top, a.Edge);
    }

    [Fact]
    public void Static_helpers_set_edge_correctly()
    {
        Assert.Equal(PortEdge.Left,   PortAnchor.Left(0.5).Edge);
        Assert.Equal(PortEdge.Top,    PortAnchor.Top(0.5).Edge);
        Assert.Equal(PortEdge.Right,  PortAnchor.Right(0.5).Edge);
        Assert.Equal(PortEdge.Bottom, PortAnchor.Bottom(0.5).Edge);
    }

    [Fact]
    public void Value_equality_by_components()
    {
        Assert.Equal(new PortAnchor(PortEdge.Top, 0.5), new PortAnchor(PortEdge.Top, 0.5));
        Assert.NotEqual(new PortAnchor(PortEdge.Top, 0.5), new PortAnchor(PortEdge.Bottom, 0.5));
        Assert.NotEqual(new PortAnchor(PortEdge.Top, 0.5), new PortAnchor(PortEdge.Top, 0.6));
    }
}
```

- [ ] **Step 2: Run tests, confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~PortAnchorTests"`
Expected: build fails with "PortAnchor / PortEdge does not exist."

- [ ] **Step 3: Create `PortEdge.cs`**

```csharp
// src/NodiumGraph/Model/PortEdge.cs
namespace NodiumGraph.Model;

public enum PortEdge { Left, Top, Right, Bottom }
```

- [ ] **Step 4: Create `PortAnchor.cs`**

```csharp
// src/NodiumGraph/Model/PortAnchor.cs
using System;

namespace NodiumGraph.Model;

public readonly record struct PortAnchor(PortEdge Edge, double Fraction)
{
    public PortEdge Edge { get; }   = ValidateEdge(Edge);
    public double Fraction { get; } = ValidateFraction(Fraction);

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

- [ ] **Step 5: Run tests, confirm green**

Run: `dotnet test --filter "FullyQualifiedName~PortAnchorTests"`
Expected: all `PortAnchorTests` pass; full `dotnet build` clean.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/PortEdge.cs src/NodiumGraph/Model/PortAnchor.cs tests/NodiumGraph.Tests/PortAnchorTests.cs
git commit -m "feat(model): add PortEdge enum and PortAnchor validated record struct"
```

---

## Task 2: `INodeShape` extensions + `Node` forwarders + `Shape` null-guard

**Goal:** Extend the interface contract and `Node`'s dispatch surface. Use default-throwing interface methods so the build stays green before each shape is updated.

**Files:**
- Modify: `src/NodiumGraph/Model/INodeShape.cs`
- Modify: `src/NodiumGraph/Model/Node.cs`
- Modify: `tests/NodiumGraph.Tests/NodeTests.cs` (create if absent)

**Acceptance Criteria:**
- [ ] `INodeShape` has `GetEdgePoint(PortAnchor, double, double)`, `GetEdgeOutwardNormal(PortAnchor, double, double)`, `InferAnchor(Point, double, double)`. Default implementations throw `NotSupportedException`.
- [ ] `Node` exposes 4 forwarding wrappers (`GetEdgePoint`, `GetEdgeOutwardNormal`, `InferAnchor`, `GetNearestBoundaryPoint`) that pass `Width`, `Height` to the shape.
- [ ] `Node.Shape` setter rejects `null` via `ArgumentNullException.ThrowIfNull(value)` and raises `PropertyChanged(nameof(Shape))` on change.

**Verify:** `dotnet test --filter "FullyQualifiedName~NodeTests"` → new tests pass; build clean (because every shape still has only `GetNearestBoundaryPoint`, the default throwers are inert in current call sites).

**Steps:**

- [ ] **Step 1: Write failing `NodeTests`**

```csharp
// tests/NodiumGraph.Tests/NodeTests.cs — extend or create
using System.ComponentModel;
using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeTests
{
    [Fact]
    public void Shape_setter_throws_on_null()
    {
        var node = new Node();
        Assert.Throws<ArgumentNullException>(() => node.Shape = null!);
    }

    [Fact]
    public void Shape_setter_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = 0;
        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Node.Shape)) fired++;
        };
        node.Shape = new EllipseShape();
        Assert.Equal(1, fired);
    }

    [Fact]
    public void GetEdgePoint_forwards_to_shape_with_dimensions()
    {
        var node = new Node { /* Width, Height — see existing Node tests for setting via internal API */ };
        // Forwarding behavior is verified per-shape in Tasks 3–5; here we just confirm Node calls through.
        // Use a stub INodeShape if needed.
        // (Concrete assertions land in Tasks 3–5.)
    }
}
```

(The `GetEdgePoint_forwards_to_shape_with_dimensions` test is a smoke check — the real shape behavior is tested per shape. If the test would require setting `Width`/`Height` via reflection or test-helpers, use a stub `INodeShape` that records the arguments it was called with.)

- [ ] **Step 2: Update `INodeShape`**

```csharp
// src/NodiumGraph/Model/INodeShape.cs
using Avalonia;

namespace NodiumGraph.Model;

public interface INodeShape
{
    Point GetNearestBoundaryPoint(Point centerRelative, double width, double height);

    // Default implementations throw — shapes must override.
    // Section A of the design doc defines per-shape semantics.
    Point GetEdgePoint(PortAnchor anchor, double width, double height)
        => throw new NotSupportedException($"{GetType().Name} does not implement GetEdgePoint.");

    Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height)
        => throw new NotSupportedException($"{GetType().Name} does not implement GetEdgeOutwardNormal.");

    PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
        => throw new NotSupportedException($"{GetType().Name} does not implement InferAnchor.");
}
```

- [ ] **Step 3: Update `Node` with forwarders + null-guard**

Locate the current `Shape` property in `src/NodiumGraph/Model/Node.cs` (around line 56–66). Replace with:

```csharp
private INodeShape _shape = new RectangleShape();

public INodeShape Shape
{
    get => _shape;
    set
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetField(ref _shape, value)) { /* INPC handled by SetField */ }
    }
}

public Point  GetEdgePoint(PortAnchor anchor)               => Shape.GetEdgePoint(anchor, Width, Height);
public Vector GetEdgeOutwardNormal(PortAnchor anchor)       => Shape.GetEdgeOutwardNormal(anchor, Width, Height);
public PortAnchor InferAnchor(Point boundaryLocal)          => Shape.InferAnchor(boundaryLocal, Width, Height);
public Point  GetNearestBoundaryPoint(Point centerRelative) => Shape.GetNearestBoundaryPoint(centerRelative, Width, Height);
```

Confirm `SetField` fires INPC (it does in the existing implementation — used by other properties on `Node`).

- [ ] **Step 4: Run NodeTests, confirm green**

Run: `dotnet test --filter "FullyQualifiedName~NodeTests"`
Expected: both null-guard and PropertyChanged tests pass.

- [ ] **Step 5: Run full build, confirm green**

Run: `dotnet build`
Expected: clean. The default-throwing methods aren't called yet — they fire only when a future Task wires `Port.Position` to them.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/INodeShape.cs src/NodiumGraph/Model/Node.cs tests/NodiumGraph.Tests/NodeTests.cs
git commit -m "feat(model): extend INodeShape with anchor-aware methods; Node forwards and rejects null Shape"
```

---

## Task 3: `RectangleShape` implementation + tests

**Goal:** Implement the three new methods on `RectangleShape` per Section A's per-edge table. Round-trip + zero-dim + canonical-corner tests.

**Files:**
- Modify: `src/NodiumGraph/Model/RectangleShape.cs`
- Modify: `tests/NodiumGraph.Tests/RectangleShapeTests.cs`

**Acceptance Criteria:**
- [ ] `GetEdgePoint(PortAnchor.Top(0), w, h)` → `(0, 0)`. `Top(1)` → `(w, 0)`. `Right(0)` → `(w, 0)`. `Right(1)` → `(w, h)`. `Bottom(0)` → `(w, h)`. `Bottom(1)` → `(0, h)`. `Left(0)` → `(0, h)`. `Left(1)` → `(0, 0)`. Midpoints land at midpoints.
- [ ] `GetEdgeOutwardNormal` returns cardinal unit vectors per edge regardless of `Fraction`.
- [ ] `InferAnchor((0, 0), w, h)` → `PortAnchor.Top(0)` (canonical at top-left per the clockwise rule).
- [ ] Round-trip: for `Fraction ∈ {0, 0.25, 0.5, 0.75, 1}` on each edge, `GetEdgePoint → InferAnchor` returns the canonical anchor; `InferAnchor → GetEdgePoint` returns the same boundary point within `1e-9`.
- [ ] Zero-dim contract: `GetEdgePoint(any anchor, 0, 0)` → `(0, 0)`; `GetEdgeOutwardNormal(Right(_), 0, 0)` → `(1, 0)`; `InferAnchor(any, 0, 0)` throws `InvalidOperationException`.

**Verify:** `dotnet test --filter "FullyQualifiedName~RectangleShapeTests"` → all tests pass.

**Steps:**

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/NodiumGraph.Tests/RectangleShapeTests.cs — extend
using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public partial class RectangleShapeTests
{
    private static readonly RectangleShape Shape = new();
    private const double W = 100, H = 60;

    // GetEdgePoint
    [Theory]
    [InlineData(PortEdge.Top,    0.0,   0.0,   0.0)]
    [InlineData(PortEdge.Top,    1.0, 100.0,   0.0)]
    [InlineData(PortEdge.Top,    0.5,  50.0,   0.0)]
    [InlineData(PortEdge.Right,  0.0, 100.0,   0.0)]
    [InlineData(PortEdge.Right,  1.0, 100.0,  60.0)]
    [InlineData(PortEdge.Right,  0.5, 100.0,  30.0)]
    [InlineData(PortEdge.Bottom, 0.0, 100.0,  60.0)]
    [InlineData(PortEdge.Bottom, 1.0,   0.0,  60.0)]
    [InlineData(PortEdge.Bottom, 0.5,  50.0,  60.0)]
    [InlineData(PortEdge.Left,   0.0,   0.0,  60.0)]
    [InlineData(PortEdge.Left,   1.0,   0.0,   0.0)]
    [InlineData(PortEdge.Left,   0.5,   0.0,  30.0)]
    public void GetEdgePoint_matches_table(PortEdge edge, double f, double expectedX, double expectedY)
    {
        var p = Shape.GetEdgePoint(new PortAnchor(edge, f), W, H);
        Assert.Equal(expectedX, p.X, 9);
        Assert.Equal(expectedY, p.Y, 9);
    }

    // GetEdgeOutwardNormal
    [Theory]
    [InlineData(PortEdge.Left,   -1.0,  0.0)]
    [InlineData(PortEdge.Top,     0.0, -1.0)]
    [InlineData(PortEdge.Right,   1.0,  0.0)]
    [InlineData(PortEdge.Bottom,  0.0,  1.0)]
    public void GetEdgeOutwardNormal_is_cardinal_unit_vector(PortEdge edge, double nx, double ny)
    {
        var n = Shape.GetEdgeOutwardNormal(new PortAnchor(edge, 0.5), W, H);
        Assert.Equal(nx, n.X, 9);
        Assert.Equal(ny, n.Y, 9);
    }

    // InferAnchor — canonical at corners
    [Theory]
    [InlineData(  0.0,   0.0, PortEdge.Top,    0.0)] // top-left → Top(0)
    [InlineData(100.0,   0.0, PortEdge.Right,  0.0)] // top-right → Right(0)
    [InlineData(100.0,  60.0, PortEdge.Bottom, 0.0)] // bottom-right → Bottom(0)
    [InlineData(  0.0,  60.0, PortEdge.Left,   0.0)] // bottom-left → Left(0)
    public void InferAnchor_canonicalizes_corners(double x, double y, PortEdge edge, double f)
    {
        var a = Shape.InferAnchor(new Point(x, y), W, H);
        Assert.Equal(edge, a.Edge);
        Assert.Equal(f, a.Fraction, 9);
    }

    // Round-trip
    [Theory]
    [InlineData(PortEdge.Top,    0.25)]
    [InlineData(PortEdge.Top,    0.75)]
    [InlineData(PortEdge.Right,  0.5)]
    [InlineData(PortEdge.Bottom, 0.3)]
    [InlineData(PortEdge.Left,   0.8)]
    public void Anchor_point_anchor_roundtrip_for_canonical_anchors(PortEdge edge, double f)
    {
        var a = new PortAnchor(edge, f);
        var p = Shape.GetEdgePoint(a, W, H);
        var inferred = Shape.InferAnchor(p, W, H);
        Assert.Equal(a, inferred);
    }

    // Non-canonical Fraction=1 → canonicalized to next edge's Fraction=0, same point
    [Theory]
    [InlineData(PortEdge.Top,    1.0, PortEdge.Right,  0.0)]
    [InlineData(PortEdge.Right,  1.0, PortEdge.Bottom, 0.0)]
    [InlineData(PortEdge.Bottom, 1.0, PortEdge.Left,   0.0)]
    [InlineData(PortEdge.Left,   1.0, PortEdge.Top,    0.0)]
    public void NonCanonical_Fraction1_canonicalizes(PortEdge fromEdge, double f, PortEdge canonEdge, double canonF)
    {
        var a = new PortAnchor(fromEdge, f);
        var p = Shape.GetEdgePoint(a, W, H);
        var inferred = Shape.InferAnchor(p, W, H);
        Assert.Equal(canonEdge, inferred.Edge);
        Assert.Equal(canonF, inferred.Fraction, 9);
        // Boundary point preserved either way:
        var p2 = Shape.GetEdgePoint(inferred, W, H);
        Assert.Equal(p.X, p2.X, 9);
        Assert.Equal(p.Y, p2.Y, 9);
    }

    // Zero-dimension contract
    [Fact]
    public void GetEdgePoint_at_zero_size_returns_origin()
    {
        var p = Shape.GetEdgePoint(PortAnchor.Right(0.5), 0, 0);
        Assert.Equal(0.0, p.X);
        Assert.Equal(0.0, p.Y);
    }

    [Theory]
    [InlineData(PortEdge.Left,   -1.0,  0.0)]
    [InlineData(PortEdge.Top,     0.0, -1.0)]
    [InlineData(PortEdge.Right,   1.0,  0.0)]
    [InlineData(PortEdge.Bottom,  0.0,  1.0)]
    public void GetEdgeOutwardNormal_at_zero_size_returns_cardinal(PortEdge edge, double nx, double ny)
    {
        var n = Shape.GetEdgeOutwardNormal(new PortAnchor(edge, 0.5), 0, 0);
        Assert.Equal(nx, n.X);
        Assert.Equal(ny, n.Y);
    }

    [Fact]
    public void InferAnchor_at_zero_size_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Shape.InferAnchor(new Point(0, 0), 0, 0));
    }
}
```

- [ ] **Step 2: Run tests, confirm they fail (default-throwing impl)**

Run: `dotnet test --filter "FullyQualifiedName~RectangleShapeTests"`
Expected: new tests FAIL with `NotSupportedException` from the default interface methods.

- [ ] **Step 3: Implement on `RectangleShape`**

```csharp
// src/NodiumGraph/Model/RectangleShape.cs — add to the existing class
public Point GetEdgePoint(PortAnchor anchor, double width, double height) => anchor.Edge switch
{
    PortEdge.Top    => new Point(anchor.Fraction * width, 0),
    PortEdge.Right  => new Point(width, anchor.Fraction * height),
    PortEdge.Bottom => new Point((1.0 - anchor.Fraction) * width, height),
    PortEdge.Left   => new Point(0, (1.0 - anchor.Fraction) * height),
    _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
};

public Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height) => anchor.Edge switch
{
    PortEdge.Left   => new Vector(-1, 0),
    PortEdge.Top    => new Vector(0, -1),
    PortEdge.Right  => new Vector(1, 0),
    PortEdge.Bottom => new Vector(0, 1),
    _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
};

public PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
{
    if (width <= 0 || height <= 0)
        throw new InvalidOperationException("InferAnchor requires positive dimensions.");

    // Canonical assignment (clockwise rule, design Section A):
    // top-left → Top(0); top-right → Right(0); bottom-right → Bottom(0); bottom-left → Left(0).
    var x = boundaryLocal.X;
    var y = boundaryLocal.Y;

    // Exact-corner short circuits.
    if (x == 0    && y == 0)      return new PortAnchor(PortEdge.Top,    0.0);
    if (x == width && y == 0)     return new PortAnchor(PortEdge.Right,  0.0);
    if (x == width && y == height)return new PortAnchor(PortEdge.Bottom, 0.0);
    if (x == 0    && y == height) return new PortAnchor(PortEdge.Left,   0.0);

    // Edge regions — boundary point known to lie on the rectangle.
    if (y == 0)      return new PortAnchor(PortEdge.Top,    x / width);
    if (x == width)  return new PortAnchor(PortEdge.Right,  y / height);
    if (y == height) return new PortAnchor(PortEdge.Bottom, (width - x) / width);
    if (x == 0)      return new PortAnchor(PortEdge.Left,   (height - y) / height);

    // Off-boundary: snap to nearest edge, then canonicalize fraction.
    // (Used by DynamicPortProvider via FindNearestBoundaryPoint, which already snaps to boundary.)
    var distTop    = y;
    var distBottom = height - y;
    var distLeft   = x;
    var distRight  = width - x;
    var min = Math.Min(Math.Min(distTop, distBottom), Math.Min(distLeft, distRight));
    if (min == distTop)    return new PortAnchor(PortEdge.Top,    Math.Clamp(x / width, 0, 1));
    if (min == distRight)  return new PortAnchor(PortEdge.Right,  Math.Clamp(y / height, 0, 1));
    if (min == distBottom) return new PortAnchor(PortEdge.Bottom, Math.Clamp((width - x) / width, 0, 1));
    return new PortAnchor(PortEdge.Left, Math.Clamp((height - y) / height, 0, 1));
}
```

- [ ] **Step 4: Run RectangleShapeTests, confirm all green**

Run: `dotnet test --filter "FullyQualifiedName~RectangleShapeTests"`
Expected: all tests pass.

- [ ] **Step 5: Confirm no regressions elsewhere**

Run: `dotnet build && dotnet test`
Expected: build clean; the only tests touching new behavior are `RectangleShapeTests` and `PortAnchorTests` — everything else should be unaffected.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/RectangleShape.cs tests/NodiumGraph.Tests/RectangleShapeTests.cs
git commit -m "feat(model): implement anchor-aware methods on RectangleShape"
```

---

## Task 4: `EllipseShape` implementation + tests

**Goal:** Implement aspect-aware ellipse boundary math per Section A's per-edge angle table.

**Files:**
- Modify: `src/NodiumGraph/Model/EllipseShape.cs`
- Modify: `tests/NodiumGraph.Tests/EllipseShapeTests.cs`

**Acceptance Criteria:**
- [ ] Per-edge angle ranges from Section A's table: `Top` `θ = -3π/4 + f·π/2`, `Right` `θ = -π/4 + f·π/2`, `Bottom` `θ = π/4 + f·π/2`, `Left` `θ = 3π/4 + f·π/2`. Each edge's `Fraction = 0` lands at the corresponding corner-midpoint per the canonical table.
- [ ] `GetEdgeOutwardNormal` returns the **aspect-aware** outward normal: at `(a·cosθ, b·sinθ)` center-relative with `a = w/2`, `b = h/2`, the unit normal is `(b·cosθ, a·sinθ)` normalized. A 200×100 ellipse and a 100×100 circle return *different* normals at the same `(edge, fraction)`.
- [ ] Round-trip for canonical anchors (Fraction ∈ (0, 1) on each edge) within `1e-9`.
- [ ] Shared endpoints (`Top(1) == Right(0)`, etc.) produce the same boundary point; `InferAnchor` returns the canonical (`Right(0)`).
- [ ] Zero-dim contract per Task 3.

**Verify:** `dotnet test --filter "FullyQualifiedName~EllipseShapeTests"`.

**Steps:**

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/NodiumGraph.Tests/EllipseShapeTests.cs — extend
using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public partial class EllipseShapeTests
{
    private static readonly EllipseShape Shape = new();

    // Per-edge corner-midpoint endpoints. Node-local coordinates with center at (w/2, h/2).
    [Theory]
    // Fraction = 0 starts (canonical):
    [InlineData(PortEdge.Top,    0.0, 200, 100,  29.289, 14.645)]  // -3π/4: (a + a·cos(-3π/4), b + b·sin(-3π/4)) = (100 - 70.711, 50 - 35.355)
    [InlineData(PortEdge.Right,  0.0, 200, 100, 170.711, 14.645)]  // -π/4:  (100 + 70.711, 50 - 35.355)
    [InlineData(PortEdge.Bottom, 0.0, 200, 100, 170.711, 85.355)]  // π/4:   (100 + 70.711, 50 + 35.355)
    [InlineData(PortEdge.Left,   0.0, 200, 100,  29.289, 85.355)]  // 3π/4:  (100 - 70.711, 50 + 35.355)
    // Midpoints of each edge:
    [InlineData(PortEdge.Top,    0.5, 200, 100, 100.0,   0.0)]
    [InlineData(PortEdge.Right,  0.5, 200, 100, 200.0,  50.0)]
    [InlineData(PortEdge.Bottom, 0.5, 200, 100, 100.0, 100.0)]
    [InlineData(PortEdge.Left,   0.5, 200, 100,   0.0,  50.0)]
    public void GetEdgePoint_matches_angle_table(PortEdge edge, double f, double w, double h, double expectedX, double expectedY)
    {
        var p = Shape.GetEdgePoint(new PortAnchor(edge, f), w, h);
        Assert.Equal(expectedX, p.X, 3);
        Assert.Equal(expectedY, p.Y, 3);
    }

    [Fact]
    public void Aspect_aware_outward_normal_differs_from_circle()
    {
        // At (Right, 0) — top-right corner-midpoint, θ = -π/4
        var ellipse200x100 = Shape.GetEdgeOutwardNormal(new PortAnchor(PortEdge.Right, 0.0), 200, 100);
        var circle100x100  = Shape.GetEdgeOutwardNormal(new PortAnchor(PortEdge.Right, 0.0), 100, 100);
        // Circle: (cos(-π/4), sin(-π/4)) ≈ (0.707, -0.707).
        // 200×100 ellipse: (b·cos(-π/4), a·sin(-π/4)) = (50·0.707, 100·-0.707) = (35.36, -70.71), normalized ≈ (0.447, -0.894).
        Assert.NotEqual(ellipse200x100.X, circle100x100.X, 3);
        Assert.NotEqual(ellipse200x100.Y, circle100x100.Y, 3);
        Assert.Equal(0.447, ellipse200x100.X, 3);
        Assert.Equal(-0.894, ellipse200x100.Y, 3);
        Assert.Equal(0.707, circle100x100.X, 3);
        Assert.Equal(-0.707, circle100x100.Y, 3);
    }

    [Theory]
    [InlineData(PortEdge.Top,    0.25)]
    [InlineData(PortEdge.Right,  0.5)]
    [InlineData(PortEdge.Bottom, 0.75)]
    [InlineData(PortEdge.Left,   0.3)]
    public void Anchor_point_anchor_roundtrip_for_canonical(PortEdge edge, double f)
    {
        var a = new PortAnchor(edge, f);
        var p = Shape.GetEdgePoint(a, 200, 100);
        var back = Shape.InferAnchor(p, 200, 100);
        Assert.Equal(a, back);
    }

    [Fact]
    public void Shared_corner_canonicalizes_to_next_edge_start()
    {
        // Top(1) and Right(0) refer to the same corner-midpoint point.
        var pTop1   = Shape.GetEdgePoint(new PortAnchor(PortEdge.Top,   1.0), 200, 100);
        var pRight0 = Shape.GetEdgePoint(new PortAnchor(PortEdge.Right, 0.0), 200, 100);
        Assert.Equal(pTop1.X, pRight0.X, 9);
        Assert.Equal(pTop1.Y, pRight0.Y, 9);

        var canonical = Shape.InferAnchor(pTop1, 200, 100);
        Assert.Equal(PortEdge.Right, canonical.Edge);
        Assert.Equal(0.0, canonical.Fraction, 9);
    }

    [Fact]
    public void GetEdgePoint_at_zero_size_returns_origin()
    {
        var p = Shape.GetEdgePoint(PortAnchor.Right(0.5), 0, 0);
        Assert.Equal(0, p.X);
        Assert.Equal(0, p.Y);
    }

    [Fact]
    public void GetEdgeOutwardNormal_at_zero_size_returns_cardinal()
    {
        var n = Shape.GetEdgeOutwardNormal(PortAnchor.Right(0.5), 0, 0);
        Assert.Equal(1, n.X);
        Assert.Equal(0, n.Y);
    }

    [Fact]
    public void InferAnchor_at_zero_size_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Shape.InferAnchor(new Point(0, 0), 0, 0));
    }
}
```

- [ ] **Step 2: Run tests, confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~EllipseShapeTests"`
Expected: new tests fail; existing tests in the partial class still pass.

- [ ] **Step 3: Implement on `EllipseShape`**

```csharp
// src/NodiumGraph/Model/EllipseShape.cs — add to the existing class
public Point GetEdgePoint(PortAnchor anchor, double width, double height)
{
    var a = width  / 2.0;
    var b = height / 2.0;
    var theta = ThetaFor(anchor);
    return new Point(a + a * Math.Cos(theta), b + b * Math.Sin(theta));
}

public Vector GetEdgeOutwardNormal(PortAnchor anchor, double width, double height)
{
    if (width <= 0 || height <= 0)
    {
        // Zero-dim contract: cardinal fallback.
        return anchor.Edge switch
        {
            PortEdge.Left   => new Vector(-1, 0),
            PortEdge.Top    => new Vector( 0, -1),
            PortEdge.Right  => new Vector( 1, 0),
            PortEdge.Bottom => new Vector( 0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
        };
    }
    var a = width  / 2.0;
    var b = height / 2.0;
    var theta = ThetaFor(anchor);
    // Outward normal direction: (b·cosθ, a·sinθ). Normalize.
    var nx = b * Math.Cos(theta);
    var ny = a * Math.Sin(theta);
    var mag = Math.Sqrt(nx * nx + ny * ny);
    return new Vector(nx / mag, ny / mag);
}

public PortAnchor InferAnchor(Point boundaryLocal, double width, double height)
{
    if (width <= 0 || height <= 0)
        throw new InvalidOperationException("InferAnchor requires positive dimensions.");

    var a = width  / 2.0;
    var b = height / 2.0;
    // Center-relative; recover θ via atan2(y/b, x/a).
    var x = boundaryLocal.X - a;
    var y = boundaryLocal.Y - b;
    var theta = Math.Atan2(y / b, x / a);

    // Canonical clockwise edge assignment, design Section A:
    //   Top:    θ ∈ [-3π/4, -π/4)  (and Top(1) at θ = -π/4 canonicalizes to Right(0))
    //   Right:  θ ∈ [-π/4,  π/4)
    //   Bottom: θ ∈ [ π/4, 3π/4)
    //   Left:   θ ∈ [ 3π/4, 5π/4)  — wraps; in atan2 terms equivalent to θ ≥ 3π/4 || θ < -3π/4.
    const double EpsCorner = 1e-9;

    // Canonical corners: exact endpoints go to the start of the next edge.
    if (Math.Abs(theta - (-3.0 * Math.PI / 4.0)) < EpsCorner) return new PortAnchor(PortEdge.Top,    0.0);
    if (Math.Abs(theta - (-1.0 * Math.PI / 4.0)) < EpsCorner) return new PortAnchor(PortEdge.Right,  0.0);
    if (Math.Abs(theta - ( 1.0 * Math.PI / 4.0)) < EpsCorner) return new PortAnchor(PortEdge.Bottom, 0.0);
    if (Math.Abs(theta - ( 3.0 * Math.PI / 4.0)) < EpsCorner) return new PortAnchor(PortEdge.Left,   0.0);

    if (theta >= -3.0 * Math.PI / 4.0 && theta < -1.0 * Math.PI / 4.0)
        return new PortAnchor(PortEdge.Top,    (theta + 3.0 * Math.PI / 4.0) / (Math.PI / 2.0));
    if (theta >= -1.0 * Math.PI / 4.0 && theta <  1.0 * Math.PI / 4.0)
        return new PortAnchor(PortEdge.Right,  (theta + 1.0 * Math.PI / 4.0) / (Math.PI / 2.0));
    if (theta >=  1.0 * Math.PI / 4.0 && theta <  3.0 * Math.PI / 4.0)
        return new PortAnchor(PortEdge.Bottom, (theta - 1.0 * Math.PI / 4.0) / (Math.PI / 2.0));
    // Left: theta ≥ 3π/4 OR theta < -3π/4 (atan2 wraps at ±π)
    var thetaLeft = theta >= 0 ? theta : theta + 2.0 * Math.PI; // normalize to [0, 2π)
    return new PortAnchor(PortEdge.Left, (thetaLeft - 3.0 * Math.PI / 4.0) / (Math.PI / 2.0));
}

private static double ThetaFor(PortAnchor anchor) => anchor.Edge switch
{
    PortEdge.Top    => -3.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
    PortEdge.Right  => -1.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
    PortEdge.Bottom =>  1.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
    PortEdge.Left   =>  3.0 * Math.PI / 4.0 + anchor.Fraction * (Math.PI / 2.0),
    _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
};
```

- [ ] **Step 4: Run tests, confirm green**

Run: `dotnet test --filter "FullyQualifiedName~EllipseShapeTests"`
Expected: all tests pass.

- [ ] **Step 5: Run full build + tests**

Run: `dotnet build && dotnet test`
Expected: build clean; only Tasks 1, 2, 3, 4 surfaces tested so far.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/EllipseShape.cs tests/NodiumGraph.Tests/EllipseShapeTests.cs
git commit -m "feat(model): implement anchor-aware methods on EllipseShape with aspect-aware normal"
```

---

## Task 5: `RoundedRectangleShape` implementation + tests

**Goal:** Implement per-edge arc-length parameterization. Corner-arc round-trip; capsule cases.

**Files:**
- Modify: `src/NodiumGraph/Model/RoundedRectangleShape.cs`
- Modify: `tests/NodiumGraph.Tests/RoundedRectangleShapeTests.cs`

**Acceptance Criteria:**
- [ ] Per-edge length matches Section A: `flatSegment = max(0, edgeLength − 2·rEff)`, where `rEff = min(r, w/2, h/2)`; total edge length is `flatSegment + π·rEff / 2`.
- [ ] Mid-flat points produce cardinal normals (`Top(0.5)` on a typical RRect → `(0, -1)`); corner-arc points produce radial normals from the corner center.
- [ ] Corner-arc round-trip: boundary points on a rounded-corner arc round-trip exactly via `InferAnchor → GetEdgePoint`.
- [ ] Horizontal capsule (`w > h`, `r = h/2`): `Left`/`Right` flat = 0 (two half-arcs joined at `Fraction = 0.5`); `Top`/`Bottom` keep `w − h` flat.
- [ ] Square + large radius (`r ≥ w/2`): all four edges have zero flat; full boundary still covered.
- [ ] Zero-dim contract per Task 3.

**Verify:** `dotnet test --filter "FullyQualifiedName~RoundedRectangleShapeTests"`.

**Steps:**

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/NodiumGraph.Tests/RoundedRectangleShapeTests.cs — extend
using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public partial class RoundedRectangleShapeTests
{
    // Note: RoundedRectangleShape currently takes a radius via ctor or property — confirm against existing source.
    private static RoundedRectangleShape MakeShape(double radius) => new(radius);

    [Fact]
    public void Mid_flat_returns_cardinal_normal()
    {
        var shape = MakeShape(10.0);
        var n = shape.GetEdgeOutwardNormal(PortAnchor.Top(0.5), 200, 100);
        Assert.Equal(0, n.X, 9);
        Assert.Equal(-1, n.Y, 9);
    }

    [Fact]
    public void Corner_arc_normal_is_radial_from_corner_center()
    {
        // r = 20, w = 200, h = 100. Top-right corner center is at (180, 20).
        // The Top edge's half of the corner is the inner half of the top-right arc.
        // At Top(1) we're at the boundary between Top and Right — exact corner-midpoint of TR arc.
        // The corner-midpoint is at angle -π/4 from the corner center: (180 + 20·cos(-π/4), 20 + 20·sin(-π/4)) ≈ (194.14, 5.86).
        var shape = MakeShape(20.0);
        var p = shape.GetEdgePoint(new PortAnchor(PortEdge.Top, 1.0), 200, 100);
        Assert.Equal(194.142, p.X, 3);
        Assert.Equal(5.858, p.Y, 3);
        // Outward normal at that point points radially: (cos(-π/4), sin(-π/4)) = (0.707, -0.707).
        var n = shape.GetEdgeOutwardNormal(new PortAnchor(PortEdge.Top, 1.0), 200, 100);
        Assert.Equal(0.707, n.X, 3);
        Assert.Equal(-0.707, n.Y, 3);
    }

    [Theory]
    [InlineData(PortEdge.Top,    0.0)]
    [InlineData(PortEdge.Top,    0.25)]
    [InlineData(PortEdge.Top,    0.5)]
    [InlineData(PortEdge.Top,    0.75)]
    [InlineData(PortEdge.Right,  0.4)]
    [InlineData(PortEdge.Bottom, 0.6)]
    [InlineData(PortEdge.Left,   0.3)]
    public void Roundtrip_for_canonical_anchors(PortEdge edge, double f)
    {
        var shape = MakeShape(15.0);
        var a = new PortAnchor(edge, f);
        var p = shape.GetEdgePoint(a, 200, 100);
        var back = shape.InferAnchor(p, 200, 100);
        Assert.Equal(a, back);
    }

    [Fact]
    public void Horizontal_capsule_LeftRight_have_zero_flat()
    {
        // Horizontal capsule: w=200, h=100, r=50 (= h/2). Left/Right flat = max(0, 100 - 100) = 0.
        // Entire Left edge is the two half-arcs. Fraction=0.5 lands at the boundary between them — the left-midpoint of the ellipse.
        var shape = MakeShape(50.0);
        var p = shape.GetEdgePoint(PortAnchor.Left(0.5), 200, 100);
        Assert.Equal(0, p.X, 3);
        Assert.Equal(50, p.Y, 3);

        // Top still has flat segment: w - 2r = 100. So Top(0.5) lands at mid-flat (100, 0).
        var pTop = shape.GetEdgePoint(PortAnchor.Top(0.5), 200, 100);
        Assert.Equal(100, pTop.X, 3);
        Assert.Equal(0, pTop.Y, 3);
    }

    [Fact]
    public void Square_with_large_radius_has_all_arc_no_flat()
    {
        // Square w=h=100, r=60. rEff = min(60, 50, 50) = 50. All edges have flat = max(0, 100 - 100) = 0.
        var shape = MakeShape(60.0);
        var p = shape.GetEdgePoint(PortAnchor.Top(0.5), 100, 100);
        // Top(0.5) lands at the midpoint between the two half-arcs → top of the circle inscribed: (50, 0).
        Assert.Equal(50, p.X, 3);
        Assert.Equal(0, p.Y, 3);
    }

    [Fact]
    public void Zero_dim_contract()
    {
        var shape = MakeShape(10.0);
        Assert.Equal(new Point(0, 0), shape.GetEdgePoint(PortAnchor.Right(0.5), 0, 0));
        Assert.Equal(new Vector(1, 0), shape.GetEdgeOutwardNormal(PortAnchor.Right(0.5), 0, 0));
        Assert.Throws<InvalidOperationException>(() => shape.InferAnchor(new Point(0, 0), 0, 0));
    }
}
```

- [ ] **Step 2: Run tests, confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~RoundedRectangleShapeTests"`
Expected: new tests fail.

- [ ] **Step 3: Implement on `RoundedRectangleShape`**

Implementation strategy (full code goes in `RoundedRectangleShape.cs`):

```csharp
// src/NodiumGraph/Model/RoundedRectangleShape.cs — add to the existing class.
//
// Per-edge structure (clockwise, Fraction = 0 at the canonical corner):
//   Top:     half of TL corner arc (135° → 90°) + flat (corner → corner) + half of TR corner arc (270° → -45° in local angle)
//   Right:   half of TR corner arc (-45° → 0°) + flat + half of BR corner arc (0° → 45°)
//   Bottom:  half of BR corner arc (45° → 90°) + flat + half of BL corner arc (90° → 135°)
//   Left:    half of BL corner arc (135° → 180°) + flat + half of TL corner arc (180° → 225° == -135°)
//
// Implementation uses three regions per edge: [0..t1] = first half-arc, [t1..t2] = flat, [t2..1] = second half-arc.
// t1 = (π·rEff / 4) / totalEdgeLength.  t2 = (π·rEff / 4 + flatLength) / totalEdgeLength.

public Point GetEdgePoint(PortAnchor anchor, double width, double height)
{
    if (width <= 0 || height <= 0) return new Point(0, 0);
    var rEff = Math.Min(Radius, Math.Min(width / 2.0, height / 2.0));
    var (edgeLen, flatLen, t1, t2) = EdgeSegments(anchor.Edge, width, height, rEff);
    var t = anchor.Fraction;

    if (t <= t1)
    {
        // First half-arc.
        var arcParam = (t / t1);                            // 0 → 1 along this half-arc
        return PointOnArc(anchor.Edge, arcRegion: 0, arcParam, width, height, rEff);
    }
    if (t <= t2)
    {
        // Flat segment.
        var flatParam = (t - t1) / (t2 - t1);
        return PointOnFlat(anchor.Edge, flatParam, width, height, rEff);
    }
    // Second half-arc.
    var arcParam2 = (t - t2) / (1.0 - t2);
    return PointOnArc(anchor.Edge, arcRegion: 1, arcParam2, width, height, rEff);
}

// PointOnFlat — linear interpolation along the flat segment in the correct clockwise direction.
// PointOnArc — quarter-circle interpolation around the appropriate corner center; arcRegion 0 = leading half, 1 = trailing half.
// EdgeSegments — returns (edgeLength, flatLength, t1, t2) per edge.
//
// GetEdgeOutwardNormal — at flat: cardinal vector for the edge. At arc: radial (point − cornerCenter) normalized.
//
// InferAnchor — locate which region (flat or one of the two arcs) the boundary-local point belongs to, then invert
// the parameterization. Use corner-center-relative angles to determine arc-fraction; clockwise progression matches
// GetEdgePoint.
```

**Implementation guidance for the agent:**
- Use four corner centers: `TL = (rEff, rEff)`, `TR = (w − rEff, rEff)`, `BR = (w − rEff, h − rEff)`, `BL = (rEff, h − rEff)`.
- Half-arcs span 45° each. The Top edge's leading half-arc is centered at `TL` and spans `θ ∈ [-3π/4, -π/2]` (clockwise, from the canonical corner-midpoint down to the top of the corner-arc on the top edge). Trailing half-arc is centered at `TR` and spans `θ ∈ [-π/2, -π/4]`.
- For `InferAnchor`: first check whether the point is within `rEff` of a corner center (corner-arc region). If yes, compute the arc-fraction; if no, it's on the flat region — handle like the rectangle case using the constrained edge.
- Watch the canonical-corner rule: at corner-midpoints (Fraction = 1 of one edge / Fraction = 0 of the next), `InferAnchor` returns the next edge's `Fraction = 0`.

Honor the existing `Radius` API on `RoundedRectangleShape` (constructor parameter or property — verify against current source).

- [ ] **Step 4: Run tests until green**

Run: `dotnet test --filter "FullyQualifiedName~RoundedRectangleShapeTests"`
Expected: all tests pass.

- [ ] **Step 5: Run full test suite**

Run: `dotnet test`
Expected: all green. No regressions in `RectangleShapeTests`, `EllipseShapeTests`, `RoundedRectangleShapeTests` (legacy `GetNearestBoundaryPoint` tests), or `PortAnchorTests`.

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/RoundedRectangleShape.cs tests/NodiumGraph.Tests/RoundedRectangleShapeTests.cs
git commit -m "feat(model): implement anchor-aware methods on RoundedRectangleShape with capsule support"
```

---

## Task 6: `TestNodes` helper + migrate 22 test files

**Goal:** Introduce a shared helper that centralizes port construction in test code, then migrate every test file using point-based `new Port(...)` to call it. Helper currently wraps the old `Point` ctor; updated in Task 7 to construct via anchor.

**Files:**
- Create: `tests/NodiumGraph.Tests/Helpers/TestNodes.cs`
- Modify: all 22 test files listed in the file map above.

**Acceptance Criteria:**
- [ ] No test file outside `TestNodes.cs` calls `new Port(...)` with a `Point`.
- [ ] All previously-passing tests still pass (semantic preservation via helper).

**Verify:** `dotnet test` → all tests pass; `grep -r "new Port(" tests/ --include="*.cs" -l` returns only `Helpers/TestNodes.cs`.

**Steps:**

- [ ] **Step 1: Create the helper**

```csharp
// tests/NodiumGraph.Tests/Helpers/TestNodes.cs
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Tests.Helpers;

internal static class TestNodes
{
    /// <summary>
    /// Constructs a Port at the given node-local boundary point. Used everywhere in tests so the
    /// implementation can shift between point-based and anchor-based without per-test churn.
    /// </summary>
    public static Port PortAt(Node owner, double x, double y, string name = "", PortFlow flow = PortFlow.Input)
        // Pre-Task-7: wrap the existing point-based ctor.
        => new Port(owner, name, flow, new Point(x, y));

    public static Port PortAt(Node owner, Point local, string name = "", PortFlow flow = PortFlow.Input)
        => PortAt(owner, local.X, local.Y, name, flow);
}
```

- [ ] **Step 2: Migrate test files**

For each test file in the 22-file list, replace `new Port(owner, ...point arguments...)` with `TestNodes.PortAt(owner, x, y, name, flow)`. Where a test passes the 2-arg `new Port(owner, point)` form, use `TestNodes.PortAt(owner, point)`.

Example migration in `GraphTests.cs`:

```diff
- var port = new Port(node, new Point(10, 5));
+ var port = TestNodes.PortAt(node, 10, 5);

- var output = new Port(node, "out", PortFlow.Output, new Point(100, 25));
+ var output = TestNodes.PortAt(node, 100, 25, "out", PortFlow.Output);
```

Add `using NodiumGraph.Tests.Helpers;` to each migrated file.

- [ ] **Step 3: Verify**

Run: `dotnet test`
Expected: all tests still pass (behavior unchanged — helper just wraps the same constructor).

Run: `grep -rn "new Port(" tests/ --include="*.cs"`
Expected: only `tests/NodiumGraph.Tests/Helpers/TestNodes.cs` shows matches.

- [ ] **Step 4: Commit**

```bash
git add tests/NodiumGraph.Tests/Helpers/TestNodes.cs tests/NodiumGraph.Tests/**/*.cs
git commit -m "refactor(tests): route port construction through TestNodes helper"
```

---

## Task 7: `Port` + provider refactor (the core API change)

**Goal:** Rewrite `Port` to be anchor-based; collapse `FixedPortProvider` and `DynamicPortProvider` accordingly; update `TestNodes` to construct via anchor inferred from the input point.

**Files:**
- Modify: `src/NodiumGraph/Model/Port.cs`
- Modify: `src/NodiumGraph/Model/FixedPortProvider.cs`
- Modify: `src/NodiumGraph/Model/DynamicPortProvider.cs`
- Modify: `tests/NodiumGraph.Tests/Helpers/TestNodes.cs`
- Modify: `tests/NodiumGraph.Tests/PortTests.cs`
- Modify: `tests/NodiumGraph.Tests/FixedPortProviderTests.cs` (delete `Implements_ILayoutAwarePortProvider`, all `UpdateLayout`/snap tests)
- Modify: `tests/NodiumGraph.Tests/DynamicPortProviderTests.cs`

**Acceptance Criteria:**
- [ ] `Port` has only one constructor: `Port(Node owner, string name, PortFlow flow, PortAnchor anchor)`. Both old point-based constructors are gone.
- [ ] `Port.Position` is get-only and derived; cached; invalidates on `Width`/`Height`/`Shape` change.
- [ ] `Port.AbsolutePosition` cache invalidates on `X`/`Y` *and* `Width`/`Height`/`Shape`.
- [ ] `Port.EmissionDirection` exists and forwards to `Owner.GetEdgeOutwardNormal(Anchor)`.
- [ ] `Port.PropertyChanged` fires for `Position` + `AbsolutePosition` + `EmissionDirection` on `Width`/`Height`/`Shape`; for `AbsolutePosition` only on `X`/`Y`.
- [ ] `FixedPortProvider` no longer implements `ILayoutAwarePortProvider`. The `layoutAware` flag, `UpdateLayout`, `LayoutInvalidated`, and per-layout state are all removed.
- [ ] `DynamicPortProvider.ResolvePort` constructs ports via `new Port(_owner, "", PortFlow.Input, _owner.InferAnchor(...))`.
- [ ] `TestNodes.PortAt(owner, x, y, ...)` infers an anchor from `(x, y)` via `owner.InferAnchor(new Point(x, y))` and constructs an anchored port.
- [ ] All existing tests pass after migration.

**Verify:** `dotnet test` → all green.

**Steps:**

- [ ] **Step 1: Write/extend `PortTests` first**

```csharp
// tests/NodiumGraph.Tests/PortTests.cs — extend
using System.ComponentModel;
using Avalonia;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public partial class PortTests
{
    [Fact]
    public void Ctor_takes_anchor()
    {
        var node = new Node();
        var port = new Port(node, "in", PortFlow.Input, PortAnchor.Left(0.5));
        Assert.Equal(PortAnchor.Left(0.5), port.Anchor);
        Assert.Equal("in", port.Name);
        Assert.Equal(PortFlow.Input, port.Flow);
    }

    [Fact]
    public void Position_invalidates_on_Width_change()
    {
        var node = new Node();
        // assume there is a test helper or internal setter to assign Width/Height for tests
        SetSize(node, 100, 50);
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.5));
        var before = port.Position;
        SetSize(node, 200, 50);
        var after = port.Position;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void EmissionDirection_fires_INPC_on_Width_change()
    {
        var node = new Node();
        SetSize(node, 100, 50);
        node.Shape = new EllipseShape();
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Right(0.25));
        var fired = 0;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Port.EmissionDirection)) fired++;
        };
        SetSize(node, 200, 50);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void EmissionDirection_does_not_fire_on_X_change()
    {
        var node = new Node();
        SetSize(node, 100, 50);
        var port = new Port(node, "", PortFlow.Input, PortAnchor.Top(0.5));
        var fired = 0;
        ((INotifyPropertyChanged)port).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Port.EmissionDirection)) fired++;
        };
        node.X = 999;
        Assert.Equal(0, fired);
    }

    // SetSize: test helper — Width/Height are internal set. Use existing pattern (reflection, friend-test assembly, or
    // a dedicated InternalsVisibleTo). Match the convention already used elsewhere in PortTests / NodeTests.
    private static void SetSize(Node n, double w, double h) { /* fill in per repo convention */ }
}
```

- [ ] **Step 2: Rewrite `Port.cs`**

```csharp
// src/NodiumGraph/Model/Port.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// A connection endpoint on a node. Position is derived from the immutable Anchor and the owner's geometry.
/// </summary>
public class Port : INotifyPropertyChanged
{
    private PortStyle? _style;
    private string? _label;
    private uint? _maxConnections;

    private Point _cachedPosition;
    private Point _cachedAbsolutePosition;
    private bool _positionDirty = true;
    private bool _absolutePositionDirty = true;
    private bool _isDetached;

    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }
    public PortAnchor Anchor { get; }

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

    public Point AbsolutePosition
    {
        get
        {
            if (_absolutePositionDirty)
            {
                var local = Position;
                _cachedAbsolutePosition = new Point(Owner.X + local.X, Owner.Y + local.Y);
                _absolutePositionDirty = false;
            }
            return _cachedAbsolutePosition;
        }
    }

    public Vector EmissionDirection => Owner.GetEdgeOutwardNormal(Anchor);

    // PortStyle, Label, MaxConnections — preserved exactly as in the existing Port.cs (no behavioral change).
    public PortStyle? Style          { get => _style; set => SetField(ref _style, value); }
    public string? Label             { get => _label; set => SetField(ref _label, value); }
    public uint? MaxConnections      { get => _maxConnections; set => SetField(ref _maxConnections, value); }

    internal void Detach()
    {
        if (_isDetached) return;
        Owner.PropertyChanged -= OnOwnerPropertyChanged;
        _isDetached = true;
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] **Step 3: Simplify `FixedPortProvider`**

```csharp
// src/NodiumGraph/Model/FixedPortProvider.cs
using System;
using System.Collections.Generic;
using Avalonia;

namespace NodiumGraph.Model;

public class FixedPortProvider : IPortProvider
{
    private const double DefaultHitRadius = 20.0;

    private readonly List<Port> _ports = new();
    private readonly double _hitRadiusSq;

    public IReadOnlyList<Port> Ports { get; }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;

    public FixedPortProvider(double hitRadius = DefaultHitRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitRadius);
        _hitRadiusSq = hitRadius * hitRadius;
        Ports = _ports.AsReadOnly();
    }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius) : this(hitRadius)
    {
        ArgumentNullException.ThrowIfNull(ports);
        foreach (var port in ports)
        {
            ArgumentNullException.ThrowIfNull(port, nameof(ports));
            _ports.Add(port);
        }
    }

    public void AddPort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        _ports.Add(port);
        PortAdded?.Invoke(port);
    }

    public bool RemovePort(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (!_ports.Remove(port)) return false;
        port.Detach();
        PortRemoved?.Invoke(port);
        return true;
    }

    public Port? ResolvePort(Point position, bool preview)
    {
        Port? closest = null;
        var closestDistSq = double.MaxValue;
        foreach (var port in _ports)
        {
            var abs = port.AbsolutePosition;
            var dx = abs.X - position.X;
            var dy = abs.Y - position.Y;
            var distSq = dx * dx + dy * dy;
            if (distSq < _hitRadiusSq && distSq < closestDistSq)
            {
                closest = port;
                closestDistSq = distSq;
            }
        }
        return closest;
    }

    public void CancelResolve() { }
}
```

- [ ] **Step 4: Update `DynamicPortProvider`**

Locate the `ResolvePort` method body. Replace the point-based port construction:

```csharp
// before:
var relative = new Point(boundary.Value.X - _owner.X, boundary.Value.Y - _owner.Y);
var port = new Port(_owner, relative);

// after:
var relative = new Point(boundary.Value.X - _owner.X, boundary.Value.Y - _owner.Y);
var anchor = _owner.InferAnchor(relative);
var port = new Port(_owner, string.Empty, PortFlow.Input, anchor);
```

Everything else in `DynamicPortProvider` stays the same.

- [ ] **Step 5: Update `TestNodes.cs`**

```csharp
// tests/NodiumGraph.Tests/Helpers/TestNodes.cs
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Tests.Helpers;

internal static class TestNodes
{
    public static Port PortAt(Node owner, double x, double y, string name = "", PortFlow flow = PortFlow.Input)
    {
        var anchor = owner.InferAnchor(new Point(x, y));
        return new Port(owner, name, flow, anchor);
    }

    public static Port PortAt(Node owner, Point local, string name = "", PortFlow flow = PortFlow.Input)
        => PortAt(owner, local.X, local.Y, name, flow);
}
```

- [ ] **Step 6: Trim `FixedPortProviderTests`**

Delete the `Implements_ILayoutAwarePortProvider` test. Delete every test that exercises `UpdateLayout` or `LayoutInvalidated` (search for those names in the file). Keep the resolution, add/remove, and round-trip-through-Position tests.

- [ ] **Step 7: Extend `DynamicPortProviderTests`**

Add a test asserting the created port's anchor round-trips to the hit point:

```csharp
[Fact]
public void ResolvePort_creates_port_whose_anchor_roundtrips_to_hit_point()
{
    var node = new Node { /* set size 200x100 via test helper */ };
    SetSize(node, 200, 100);
    var provider = new DynamicPortProvider(node);
    var hit = new Point(node.X + 150, node.Y + 100); // bottom edge, world coords
    var port = provider.ResolvePort(hit, preview: false);
    Assert.NotNil(port);
    var localBoundary = new Point(port!.AbsolutePosition.X - node.X, port.AbsolutePosition.Y - node.Y);
    Assert.Equal(localBoundary, node.GetEdgePoint(port.Anchor));
}
```

- [ ] **Step 8: Run full test suite**

Run: `dotnet test`
Expected: all green. PortTests passes; FixedPortProviderTests / DynamicPortProviderTests pass; the 22 test files migrated in Task 6 still pass (via `TestNodes.PortAt`).

- [ ] **Step 9: Commit**

```bash
git add src/NodiumGraph/Model/Port.cs src/NodiumGraph/Model/FixedPortProvider.cs src/NodiumGraph/Model/DynamicPortProvider.cs tests/NodiumGraph.Tests/Helpers/TestNodes.cs tests/NodiumGraph.Tests/PortTests.cs tests/NodiumGraph.Tests/FixedPortProviderTests.cs tests/NodiumGraph.Tests/DynamicPortProviderTests.cs
git commit -m "feat(model): switch Port to anchor-based positioning; simplify providers"
```

---

## Task 8: Delete `PortEmissionDirection`; routers use `port.EmissionDirection`

**Goal:** Remove the standalone static helper. Update all router call sites.

**Files:**
- Delete: `src/NodiumGraph/Interactions/PortEmissionDirection.cs`
- Modify: `src/NodiumGraph/Interactions/BezierRouter.cs`
- Modify: `src/NodiumGraph/Interactions/StepRouter.cs`
- Modify: `src/NodiumGraph/Interactions/StraightRouter.cs` (only if it calls Resolve)
- Modify: any router tests that referenced `PortEmissionDirection.Resolve` directly

**Acceptance Criteria:**
- [ ] No occurrence of `PortEmissionDirection` anywhere in `src/` or `tests/`.
- [ ] Routers call `port.EmissionDirection` instead of `PortEmissionDirection.Resolve(port)`.
- [ ] All router tests pass.
- [ ] New test: ellipse-node port emits the aspect-aware non-cardinal vector.

**Verify:** `dotnet test`; `grep -rn "PortEmissionDirection" src/ tests/` → empty.

**Steps:**

- [ ] **Step 1: Enumerate call sites**

Run: `grep -rn "PortEmissionDirection" src/ tests/`
Capture the list before changes.

- [ ] **Step 2: Replace call sites**

In each router file, change:
```csharp
var dir = PortEmissionDirection.Resolve(port);
```
to:
```csharp
var dir = port.EmissionDirection;
```

- [ ] **Step 3: Delete the static helper file**

```bash
git rm src/NodiumGraph/Interactions/PortEmissionDirection.cs
```

- [ ] **Step 4: Add an ellipse-emission router test**

Add to `BezierRouterTests` (or new file if cleaner):

```csharp
[Fact]
public void Bezier_emission_for_ellipse_node_is_aspect_aware()
{
    var node = new Node { /* size 200x100, shape = EllipseShape */ };
    SetSize(node, 200, 100);
    node.Shape = new EllipseShape();
    var port = new Port(node, "out", PortFlow.Output, PortAnchor.Right(0.0));
    // -π/4 on 200x100 ellipse → outward unit normal ≈ (0.447, -0.894).
    var dir = port.EmissionDirection;
    Assert.Equal(0.447, dir.X, 3);
    Assert.Equal(-0.894, dir.Y, 3);
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test`
Expected: all green; new ellipse test passes.

Run: `grep -rn "PortEmissionDirection" src/ tests/`
Expected: empty.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(routers): port emission via Port.EmissionDirection; delete static helper"
```

---

## Task 9: Canvas refactor — drop `ILayoutAwarePortProvider`; add W/H/Shape invalidation

**Goal:** Delete the layout-aware-provider interface and its canvas wiring. Add the `Width`/`Height`/`Shape` case in `OnNodePropertyChanged` that invalidates connection geometry and adornments.

**Files:**
- Delete: `src/NodiumGraph/Model/ILayoutAwarePortProvider.cs`
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Modify/add: relevant canvas tests

**Acceptance Criteria:**
- [ ] `ILayoutAwarePortProvider` is gone (no references anywhere).
- [ ] `NodiumGraphCanvas.AttachProvider` / `DetachProvider` no longer subscribe to `LayoutInvalidated`.
- [ ] `OnNodePropertyChanged` has a case for `Width` / `Height` / `Shape` that calls `InvalidateConnectionGeometryForNode(node)` + `InvalidateNodeAdornments(node)` + `InvalidateVisual()`.
- [ ] New test: resizing a node invalidates connection geometry for connections touching it.
- [ ] New test: swapping `Node.Shape` produces the same invalidation.

**Verify:** `dotnet test --filter "FullyQualifiedName~NodiumGraphCanvas"` → all green; `grep -rn "ILayoutAwarePortProvider\|LayoutInvalidated\|OnLayoutAwareProviderInvalidated" src/ tests/` → empty.

**Steps:**

- [ ] **Step 1: Delete `ILayoutAwarePortProvider.cs`**

```bash
git rm src/NodiumGraph/Model/ILayoutAwarePortProvider.cs
```

- [ ] **Step 2: Strip canvas wiring**

In `NodiumGraphCanvas.cs`:
- Remove `OnLayoutAwareProviderInvalidated` method entirely.
- In `AttachProvider`: remove the `if (provider is ILayoutAwarePortProvider lap) lap.LayoutInvalidated += OnLayoutAwareProviderInvalidated;` block (and its `else`/`null` handling if any).
- In `DetachProvider`: remove the matching unsubscription.

- [ ] **Step 3: Add `OnNodePropertyChanged` case**

Locate `OnNodePropertyChanged` (around line 1912). Add a new branch:

```csharp
else if (e.PropertyName is nameof(Node.Width) or nameof(Node.Height) or nameof(Node.Shape))
{
    if (sender is Node node)
    {
        InvalidateConnectionGeometryForNode(node);
        InvalidateNodeAdornments(node);
        InvalidateVisual();
    }
}
```

Place it before or after existing branches in a clear order (style suggestion: near the X/Y branch since they're conceptually related).

- [ ] **Step 4: Add canvas integration tests**

```csharp
// tests/NodiumGraph.Tests/Controls/NodiumGraphCanvasResizeInvalidationTests.cs — new file
using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests.Controls;

public class NodiumGraphCanvasResizeInvalidationTests
{
    [AvaloniaFact]
    public void Width_change_invalidates_connection_geometry()
    {
        // Set up a canvas with a graph: two nodes, one connection between them.
        // Cache the connection geometry by triggering hit-test or render.
        // Assert HasConnectionGeometry(connection.Id) == true.
        // Change one node's Width via the internal setter.
        // Assert HasConnectionGeometry(connection.Id) == false (cache dropped).
    }

    [AvaloniaFact]
    public void Shape_change_invalidates_connection_geometry()
    {
        // Same setup; swap Node.Shape; assert geometry cache dropped.
    }
}
```

(Implementation details for cache observation: use the existing `HasConnectionGeometry` / `ConnectionGeometryCacheCount` internal probes already in `NodiumGraphCanvas`. They are `internal` and accessible via `InternalsVisibleTo` to the test assembly.)

- [ ] **Step 5: Run tests**

Run: `dotnet test`
Expected: all green.

Run: `grep -rn "ILayoutAwarePortProvider\|LayoutInvalidated\|OnLayoutAwareProviderInvalidated" src/ tests/`
Expected: empty.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(canvas): drop ILayoutAwarePortProvider; invalidate on Width/Height/Shape"
```

---

## Task 10: Migrate sample apps

**Goal:** Update `samples/GettingStarted` and `samples/NodiumGraph.Sample` to construct ports via `PortAnchor`.

**Files:**
- Modify: every port-construction site under `samples/`.

**Acceptance Criteria:**
- [ ] No `new Port(...)` calls in `samples/` use a `Point`.
- [ ] Both sample apps build cleanly and run with no visible regression in port placement vs. current `main`.

**Verify:** `dotnet build samples/`; manual launch + smoke test.

**Steps:**

- [ ] **Step 1: Enumerate sites**

Run: `grep -rn "new Port(" samples/`
Capture the list.

- [ ] **Step 2: Migrate each site**

For each construction site, choose the appropriate anchor:
- If the port sits at a node corner or midpoint, use the static helper (e.g. `PortAnchor.Left(0.5)`).
- If the port previously used an arbitrary `Point`, compute the anchor with `node.InferAnchor(...)` *or* recompute by hand using the per-shape table in the spec.

Example:
```diff
- fp.AddPort(new Port(node, "in",  PortFlow.Input,  new Point(0, 25)));
- fp.AddPort(new Port(node, "out", PortFlow.Output, new Point(100, 25)));
+ fp.AddPort(new Port(node, "in",  PortFlow.Input,  PortAnchor.Left(0.5)));
+ fp.AddPort(new Port(node, "out", PortFlow.Output, PortAnchor.Right(0.5)));
```

- [ ] **Step 3: Build + smoke test**

Run: `dotnet build samples/GettingStarted/`
Run: `dotnet build samples/NodiumGraph.Sample/`
Run: `dotnet run --project samples/NodiumGraph.Sample` — visually confirm port placement matches `main`.

- [ ] **Step 4: Commit**

```bash
git add samples/
git commit -m "samples: migrate port construction to anchor form"
```

---

## Task 11: User guide migration + sweep

**Goal:** Update the user-guide pages enumerated in the spec; run the sweep over `docs/userguide/` to catch anything else.

**Files:**
- Modify (enumerated set):
  - `docs/userguide/1-tutorial/getting-started.md`
  - `docs/userguide/2-how-to/custom-port-provider.md`
  - `docs/userguide/2-how-to/custom-node-template.md`
  - `docs/userguide/2-how-to/style-ports.md`
  - `docs/userguide/2-how-to/persist-graph-state.md`
  - `docs/userguide/2-how-to/custom-router.md`
  - `docs/userguide/3-reference/strategies.md`
  - `docs/userguide/3-reference/model.md`
  - `docs/userguide/3-reference/rendering-pipeline.md`
- Sweep: any other file under `docs/userguide/` flagged by the grep below.

**Acceptance Criteria:**
- [ ] `grep -rn "layoutAware\|ILayoutAwarePortProvider\|PortEmissionDirection" docs/userguide/` → empty.
- [ ] `grep -rn "new Port(.*Point" docs/userguide/` → empty.
- [ ] Persistence guide records `Anchor.Edge + Anchor.Fraction` for both fixed and dynamic ports.
- [ ] `custom-router.md` notes that `Port.AbsolutePosition` invalidates on `Width`/`Height`/`Shape` change as well as node move.
- [ ] `model.md`'s `Port` constructor block reflects the new ctor: `Port(Node, string, PortFlow, PortAnchor)`.

**Verify:** Manual review of each modified page; the three greps above return empty.

**Steps:**

- [ ] **Step 1: Migrate each enumerated page**

For each page in the list:
- Replace `new Port(node, ..., new Point(x, y))` examples with `new Port(node, ..., PortAnchor.Edge(f))`.
- Drop `layoutAware: true` from `FixedPortProvider` examples; remove the surrounding explanation.
- Replace `PortEmissionDirection.Resolve(port)` (if any) with `port.EmissionDirection`.

For specific pages:
- **`persist-graph-state.md`** — update the JSON sample and persistence prose. Persist anchors as `{"edge": "Right", "fraction": 0.5}` (or whichever serialization shape fits the guide's existing style). Show both a fixed-port example and a dynamic-port example.
- **`custom-router.md` line ~180** — change the invalidation note to `"Port.AbsolutePosition is cached and invalidated when its owner node moves OR resizes OR changes Shape."`
- **`model.md` lines 100–101** — replace the two old constructor signatures with the single new one. Update the table-row prose to reference `Anchor` rather than `Position`.
- **`rendering-pipeline.md`** — where `Port.Position` is described, note that it is derived from `Port.Anchor + Owner.Width/Height/Shape`.

- [ ] **Step 2: Run the sweep greps**

Run:
```bash
grep -rn "layoutAware\|ILayoutAwarePortProvider\|PortEmissionDirection" docs/userguide/
grep -rn 'new Port(.*Point' docs/userguide/
grep -rn 'AbsolutePosition.*node moves\b' docs/userguide/
```
Expected: empty (or only the items just updated; if a grep still surfaces anything, fix that page too — this is the catch-all sweep).

- [ ] **Step 3: Commit**

```bash
git add docs/userguide/
git commit -m "docs(userguide): migrate to anchor-based port positioning"
```

---

## Done

When all tasks above are complete:
- [ ] `dotnet build` clean, no warnings.
- [ ] `dotnet test` all green.
- [ ] Sample apps build and run with no visible regression in port placement.
- [ ] Userguide sweep returns empty.
- [ ] No references to `ILayoutAwarePortProvider`, `PortEmissionDirection`, `layoutAware` flag, or `Port(Node, ..., Point)` constructors anywhere in `src/`, `tests/`, `samples/`, or `docs/userguide/`.

The spec's design constraints, contract guarantees, and breaking-change list (rev 11 of [[2026-05-13-anchor-based-port-positioning-design]]) are fully realized.
