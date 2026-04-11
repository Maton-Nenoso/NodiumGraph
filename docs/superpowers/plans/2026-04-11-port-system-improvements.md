# Port System Improvements Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers-extended-cc:subagent-driven-development (if subagents available) or superpowers-extended-cc:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify the port model, consolidate providers, unify hit-testing, add connection capacity metadata, fix DynamicPortProvider cleanup, cache AbsolutePosition, and add observable port collection changes.

**Architecture:** Clean-break redesign of the port system. Drop AnglePortProvider, refocus INodeShape from angle-based to nearest-boundary-point, add `preview` and `CancelResolve` to IPortProvider, add events for port membership changes, and wire the canvas to use unified hit-testing with rollback on failed commits.

**Tech Stack:** C# / .NET 10, Avalonia 12, xUnit v3

**Spec:** `docs/superpowers/specs/2026-04-11-port-system-improvements-design.md`

---

### Task 1: Refocus INodeShape from angle-based to nearest-boundary-point

**Files:**
- Modify: `src/NodiumGraph/Model/INodeShape.cs`
- Modify: `src/NodiumGraph/Model/RectangleShape.cs`
- Modify: `src/NodiumGraph/Model/EllipseShape.cs`
- Modify: `src/NodiumGraph/Model/RoundedRectangleShape.cs`
- Create: `tests/NodiumGraph.Tests/RectangleShapeTests.cs`
- Create: `tests/NodiumGraph.Tests/EllipseShapeTests.cs`
- Create: `tests/NodiumGraph.Tests/RoundedRectangleShapeTests.cs`

- [ ] **Step 1: Write failing tests for RectangleShape.GetNearestBoundaryPoint**

```csharp
// tests/NodiumGraph.Tests/RectangleShapeTests.cs
using NodiumGraph.Model;
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class RectangleShapeTests
{
    private const double Tolerance = 0.01;
    private readonly RectangleShape _shape = new();

    [Fact]
    public void Point_right_of_center_snaps_to_right_edge()
    {
        // Position (60, 0) center-relative, 100x80 rect -> right edge at (50, 0)
        var result = _shape.GetNearestBoundaryPoint(new Point(60, 0), 100, 80);
        Assert.Equal(50, result.X, Tolerance);
        Assert.Equal(0, result.Y, Tolerance);
    }

    [Fact]
    public void Point_above_center_snaps_to_top_edge()
    {
        var result = _shape.GetNearestBoundaryPoint(new Point(0, -50), 100, 80);
        Assert.Equal(0, result.X, Tolerance);
        Assert.Equal(-40, result.Y, Tolerance);
    }

    [Fact]
    public void Point_below_center_snaps_to_bottom_edge()
    {
        var result = _shape.GetNearestBoundaryPoint(new Point(0, 50), 100, 80);
        Assert.Equal(0, result.X, Tolerance);
        Assert.Equal(40, result.Y, Tolerance);
    }

    [Fact]
    public void Point_left_of_center_snaps_to_left_edge()
    {
        var result = _shape.GetNearestBoundaryPoint(new Point(-60, 0), 100, 80);
        Assert.Equal(-50, result.X, Tolerance);
        Assert.Equal(0, result.Y, Tolerance);
    }

    [Fact]
    public void Point_at_diagonal_snaps_to_nearest_edge()
    {
        // (60, 10) center-relative, 100x80 rect -> right edge closest
        var result = _shape.GetNearestBoundaryPoint(new Point(60, 10), 100, 80);
        Assert.Equal(50, result.X, Tolerance);
        // Y should be proportionally placed on the right edge
    }

    [Fact]
    public void Point_inside_rect_snaps_to_nearest_edge()
    {
        // (45, 0) is inside (halfW=50), nearest edge is right at (50, 0)
        var result = _shape.GetNearestBoundaryPoint(new Point(45, 0), 100, 80);
        Assert.Equal(50, result.X, Tolerance);
        Assert.Equal(0, result.Y, Tolerance);
    }

    [Fact]
    public void Point_at_center_returns_boundary_point()
    {
        // (0, 0) center -> any edge is valid, should not crash
        var result = _shape.GetNearestBoundaryPoint(new Point(0, 0), 100, 80);
        // Should be on one of the edges
        var onEdge = Math.Abs(result.X) == 50 || Math.Abs(result.Y) == 40;
        Assert.True(onEdge);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~RectangleShapeTests" --no-build 2>&1 | head -20`
Expected: FAIL — `GetNearestBoundaryPoint` does not exist

- [ ] **Step 3: Replace INodeShape.GetBoundaryPoint with GetNearestBoundaryPoint**

```csharp
// src/NodiumGraph/Model/INodeShape.cs
using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// Defines the geometric boundary of a node shape for port positioning.
/// </summary>
public interface INodeShape
{
    /// <summary>
    /// Returns the nearest point on the shape boundary to the given position.
    /// </summary>
    /// <param name="position">Center-relative coordinates (0,0 = node center).</param>
    /// <param name="width">Width of the node.</param>
    /// <param name="height">Height of the node.</param>
    /// <returns>Center-relative point on the boundary.</returns>
    Point GetNearestBoundaryPoint(Point position, double width, double height);
}
```

- [ ] **Step 4: Implement RectangleShape.GetNearestBoundaryPoint**

Replace `GetBoundaryPoint` in `src/NodiumGraph/Model/RectangleShape.cs`:

```csharp
public Point GetNearestBoundaryPoint(Point position, double width, double height)
{
    var halfW = width / 2.0;
    var halfH = height / 2.0;

    // Clamp position to rectangle bounds
    var cx = Math.Clamp(position.X, -halfW, halfW);
    var cy = Math.Clamp(position.Y, -halfH, halfH);

    // If point is outside, the clamped point is on the boundary
    if (cx != position.X || cy != position.Y)
    {
        return new Point(cx, cy);
    }

    // Point is inside: snap to nearest edge
    var distLeft = cx + halfW;
    var distRight = halfW - cx;
    var distTop = cy + halfH;
    var distBottom = halfH - cy;
    var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

    if (minDist == distRight) return new Point(halfW, cy);
    if (minDist == distLeft) return new Point(-halfW, cy);
    if (minDist == distBottom) return new Point(cx, halfH);
    return new Point(cx, -halfH);
}
```

- [ ] **Step 5: Implement EllipseShape.GetNearestBoundaryPoint**

Replace `GetBoundaryPoint` in `src/NodiumGraph/Model/EllipseShape.cs`:

```csharp
public Point GetNearestBoundaryPoint(Point position, double width, double height)
{
    var a = width / 2.0;
    var b = height / 2.0;

    if (a < 1e-12 || b < 1e-12)
        return new Point(0, 0);

    // Use the angle from center to position to find boundary point
    var angle = Math.Atan2(position.Y, position.X);
    var bx = a * Math.Cos(angle);
    var by = b * Math.Sin(angle);
    return new Point(bx, by);
}
```

- [ ] **Step 6: Implement RoundedRectangleShape.GetNearestBoundaryPoint**

Replace `GetBoundaryPoint` in `src/NodiumGraph/Model/RoundedRectangleShape.cs`. The approach: find nearest boundary point on the rectangle, then if in a corner region, project onto the corner arc instead.

```csharp
public Point GetNearestBoundaryPoint(Point position, double width, double height)
{
    var halfW = width / 2.0;
    var halfH = height / 2.0;
    var r = Math.Min(CornerRadius, Math.Min(halfW, halfH));

    if (r < 1e-12)
        return FallbackRectangle.GetNearestBoundaryPoint(position, width, height);

    // Get rectangle's nearest boundary point first
    var rectPoint = FallbackRectangle.GetNearestBoundaryPoint(position, width, height);

    // Check if point falls in a corner region
    var innerHalfW = halfW - r;
    var innerHalfH = halfH - r;

    if (Math.Abs(rectPoint.X) > innerHalfW + 1e-12 && Math.Abs(rectPoint.Y) > innerHalfH + 1e-12)
    {
        // In corner region — project onto corner arc
        var cx = Math.Sign(rectPoint.X) * innerHalfW;
        var cy = Math.Sign(rectPoint.Y) * innerHalfH;

        var dx = position.X - cx;
        var dy = position.Y - cy;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist < 1e-12)
            return new Point(cx + Math.Sign(rectPoint.X) * r, cy);

        return new Point(cx + dx / dist * r, cy + dy / dist * r);
    }

    return rectPoint;
}
```

- [ ] **Step 7: Write tests for EllipseShape and RoundedRectangleShape**

Create `tests/NodiumGraph.Tests/EllipseShapeTests.cs` and `tests/NodiumGraph.Tests/RoundedRectangleShapeTests.cs` with analogous boundary point tests.

- [ ] **Step 8: Run all tests**

Run: `dotnet test`
Expected: All shape tests PASS. AnglePortProviderTests will FAIL (uses old `GetBoundaryPoint`) — expected, we delete those in Task 3.

- [ ] **Step 9: Commit**

```bash
git add src/NodiumGraph/Model/INodeShape.cs src/NodiumGraph/Model/RectangleShape.cs src/NodiumGraph/Model/EllipseShape.cs src/NodiumGraph/Model/RoundedRectangleShape.cs tests/NodiumGraph.Tests/RectangleShapeTests.cs tests/NodiumGraph.Tests/EllipseShapeTests.cs tests/NodiumGraph.Tests/RoundedRectangleShapeTests.cs
git commit -m "Refocus INodeShape from angle-based to nearest-boundary-point"
```

---

### Task 2: Simplify Port model — remove Angle/LabelPlacement, add MaxConnections, cache AbsolutePosition

**Files:**
- Modify: `src/NodiumGraph/Model/Port.cs`
- Modify: `src/NodiumGraph/Model/PortStyle.cs`
- Delete: `src/NodiumGraph/Model/PortLabelPlacement.cs`
- Modify: `tests/NodiumGraph.Tests/PortTests.cs`
- Modify: `tests/NodiumGraph.Tests/PortStyleTests.cs`

- [ ] **Step 1: Write failing tests for new Port behavior**

Add to `tests/NodiumGraph.Tests/PortTests.cs`:

```csharp
[Fact]
public void MaxConnections_defaults_to_null()
{
    var node = new Node();
    var port = new Port(node, new Point(0, 0));
    Assert.Null(port.MaxConnections);
}

[Fact]
public void MaxConnections_can_be_set()
{
    var node = new Node();
    var port = new Port(node, new Point(0, 0));
    port.MaxConnections = 2;
    Assert.Equal((uint)2, port.MaxConnections);
}

[Fact]
public void AbsolutePosition_is_cached_and_invalidated_on_node_move()
{
    var node = new Node { X = 10, Y = 20 };
    var port = new Port(node, new Point(5, 5));
    var first = port.AbsolutePosition;
    Assert.Equal(new Point(15, 25), first);

    node.X = 100;
    var second = port.AbsolutePosition;
    Assert.Equal(new Point(105, 25), second);
}

[Fact]
public void AbsolutePosition_is_invalidated_on_position_change()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(10, 10));
    Assert.Equal(new Point(10, 10), port.AbsolutePosition);

    port.Position = new Point(20, 30);
    Assert.Equal(new Point(20, 30), port.AbsolutePosition);
}

[Fact]
public void AbsolutePosition_fires_PropertyChanged_when_node_moves()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(10, 10));

    var changedProps = new List<string?>();
    port.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

    node.X = 50;

    Assert.Contains(nameof(Port.AbsolutePosition), changedProps);
}

[Fact]
public void AbsolutePosition_fires_PropertyChanged_when_position_changes()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(10, 10));

    var changedProps = new List<string?>();
    port.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

    port.Position = new Point(20, 30);

    Assert.Contains(nameof(Port.AbsolutePosition), changedProps);
}

[Fact]
public void Detach_unsubscribes_from_owner()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(10, 10));
    _ = port.AbsolutePosition; // prime cache

    port.Detach();
    node.X = 999;

    // Cache should be stale (returns old value) since we detached
    Assert.Equal(new Point(10, 10), port.AbsolutePosition);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~PortTests"`
Expected: FAIL — `MaxConnections` and `Detach` do not exist

- [ ] **Step 3: Move PortLabelPlacement enum into PortStyle.cs, add LabelPlacement property**

In `src/NodiumGraph/Model/PortStyle.cs`, add:

```csharp
private PortLabelPlacement? _labelPlacement;

/// <summary>
/// Controls where the label is placed relative to the port visual.
/// When null, placement is auto-determined from port position.
/// </summary>
public PortLabelPlacement? LabelPlacement
{
    get => _labelPlacement;
    set => SetField(ref _labelPlacement, value);
}
```

Move the `PortLabelPlacement` enum definition into `PortStyle.cs` (same namespace, keep as a top-level enum). Delete `src/NodiumGraph/Model/PortLabelPlacement.cs`.

- [ ] **Step 4: Update Port.cs — remove Angle/LabelPlacement, add MaxConnections, cache AbsolutePosition**

```csharp
// src/NodiumGraph/Model/Port.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;

namespace NodiumGraph.Model;

public class Port : INotifyPropertyChanged
{
    private Point _position;
    private PortStyle? _style;
    private string? _label;
    private uint? _maxConnections;
    private Point _cachedAbsolutePosition;
    private bool _absolutePositionDirty = true;
    private bool _isDetached;

    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }

    public Point Position
    {
        get => _position;
        internal set
        {
            if (SetField(ref _position, value))
            {
                _absolutePositionDirty = true;
                OnPropertyChanged(nameof(AbsolutePosition));
            }
        }
    }

    public Point AbsolutePosition
    {
        get
        {
            if (_absolutePositionDirty)
            {
                _cachedAbsolutePosition = new Point(Owner.X + Position.X, Owner.Y + Position.Y);
                _absolutePositionDirty = false;
            }
            return _cachedAbsolutePosition;
        }
    }

    public PortStyle? Style
    {
        get => _style;
        set => SetField(ref _style, value);
    }

    public string? Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }

    public uint? MaxConnections
    {
        get => _maxConnections;
        set => SetField(ref _maxConnections, value);
    }

    public Port(Node owner, string name, PortFlow flow, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Flow = flow;
        _position = position;
        Owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public Port(Node owner, Point position) : this(owner, string.Empty, PortFlow.Input, position) { }

    internal void Detach()
    {
        if (_isDetached) return;
        Owner.PropertyChanged -= OnOwnerPropertyChanged;
        _isDetached = true;
    }

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Node.X) or nameof(Node.Y))
        {
            _absolutePositionDirty = true;
            OnPropertyChanged(nameof(AbsolutePosition));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- [ ] **Step 5: Remove Angle/LabelPlacement tests from PortTests.cs, update PortStyleTests.cs**

Remove the `Setting_Angle_fires_PropertyChanged`, `Setting_Angle_to_same_value_does_not_fire`, `Setting_LabelPlacement_fires_PropertyChanged`, and `Setting_LabelPlacement_to_same_value_does_not_fire` tests. Add LabelPlacement tests to `PortStyleTests.cs`.

- [ ] **Step 6: Run all tests**

Run: `dotnet test`
Expected: Port tests PASS. AnglePortProvider tests still FAIL (expected — deleted in Task 3).

- [ ] **Step 7: Commit**

```bash
git add src/NodiumGraph/Model/Port.cs src/NodiumGraph/Model/PortStyle.cs tests/NodiumGraph.Tests/PortTests.cs tests/NodiumGraph.Tests/PortStyleTests.cs
git rm src/NodiumGraph/Model/PortLabelPlacement.cs
git commit -m "Simplify Port: remove Angle/LabelPlacement, add MaxConnections, cache AbsolutePosition"
```

---

### Task 3: Update IPortProvider interface and delete AnglePortProvider

**Files:**
- Modify: `src/NodiumGraph/Model/IPortProvider.cs`
- Delete: `src/NodiumGraph/Model/AnglePortProvider.cs`
- Delete: `tests/NodiumGraph.Tests/AnglePortProviderTests.cs`

- [ ] **Step 1: Update IPortProvider interface**

```csharp
// src/NodiumGraph/Model/IPortProvider.cs
using Avalonia;

namespace NodiumGraph.Model;

public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position, bool preview);
    void CancelResolve();
    event Action<Port>? PortAdded;
    event Action<Port>? PortRemoved;
}
```

- [ ] **Step 2: Delete AnglePortProvider.cs and its tests**

```bash
git rm src/NodiumGraph/Model/AnglePortProvider.cs
git rm tests/NodiumGraph.Tests/AnglePortProviderTests.cs
```

- [ ] **Step 3: Verify build compiles (FixedPortProvider and DynamicPortProvider will break — expected, fixed in Tasks 4-5)**

Run: `dotnet build 2>&1 | head -30`
Expected: Build errors in FixedPortProvider, DynamicPortProvider, NodiumGraphCanvas, CanvasOverlay, and sample app.

- [ ] **Step 4: Commit**

```bash
git add src/NodiumGraph/Model/IPortProvider.cs
git commit -m "Update IPortProvider interface: add preview, CancelResolve, events; delete AnglePortProvider"
```

---

### Task 4: Rewrite FixedPortProvider with new interface

**Files:**
- Modify: `src/NodiumGraph/Model/FixedPortProvider.cs`
- Modify: `tests/NodiumGraph.Tests/FixedPortProviderTests.cs`

- [ ] **Step 1: Write failing tests for new FixedPortProvider behavior**

Add to `tests/NodiumGraph.Tests/FixedPortProviderTests.cs`:

```csharp
[Fact]
public void AddPort_adds_and_fires_event()
{
    var provider = new FixedPortProvider();
    var node = new Node();
    var port = new Port(node, new Point(10, 10));

    Port? addedPort = null;
    provider.PortAdded += p => addedPort = p;

    provider.AddPort(port);

    Assert.Single(provider.Ports);
    Assert.Same(port, addedPort);
}

[Fact]
public void RemovePort_removes_fires_event_and_detaches()
{
    var provider = new FixedPortProvider();
    var node = new Node();
    var port = new Port(node, new Point(10, 10));
    provider.AddPort(port);

    Port? removedPort = null;
    provider.PortRemoved += p => removedPort = p;

    var result = provider.RemovePort(port);

    Assert.True(result);
    Assert.Empty(provider.Ports);
    Assert.Same(port, removedPort);
}

[Fact]
public void ResolvePort_preview_true_returns_nearest()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(0, 0));
    var provider = new FixedPortProvider(new[] { port });
    Assert.Same(port, provider.ResolvePort(new Point(5, 5), preview: true));
}

[Fact]
public void ResolvePort_preview_false_returns_nearest()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(0, 0));
    var provider = new FixedPortProvider(new[] { port });
    Assert.Same(port, provider.ResolvePort(new Point(5, 5), preview: false));
}

[Fact]
public void CancelResolve_is_noop()
{
    var provider = new FixedPortProvider();
    provider.CancelResolve(); // Should not throw
}

[Fact]
public void UpdateLayout_repositions_ports_when_layout_aware()
{
    var node = new Node { X = 0, Y = 0 };
    // Port at top-left-relative (95, 40) — near right edge of 100x80 node
    var port = new Port(node, new Point(95, 40));
    var provider = new FixedPortProvider(new[] { port }, layoutAware: true);

    // After layout, port should snap to nearest boundary (right edge)
    provider.UpdateLayout(100, 80, new RectangleShape());

    Assert.Equal(100, port.Position.X, 0.01); // snapped to right edge
    Assert.Equal(40, port.Position.Y, 0.01);
}

[Fact]
public void UpdateLayout_does_not_reposition_when_not_layout_aware()
{
    var node = new Node { X = 0, Y = 0 };
    var port = new Port(node, new Point(95, 40));
    var provider = new FixedPortProvider(new[] { port }, layoutAware: false);

    provider.UpdateLayout(100, 80, new RectangleShape());

    // Position unchanged
    Assert.Equal(95, port.Position.X, 0.01);
    Assert.Equal(40, port.Position.Y, 0.01);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FixedPortProviderTests" --no-build 2>&1 | head -20`
Expected: FAIL — signature mismatch

- [ ] **Step 3: Implement updated FixedPortProvider**

```csharp
// src/NodiumGraph/Model/FixedPortProvider.cs
using Avalonia;

namespace NodiumGraph.Model;

public class FixedPortProvider : ILayoutAwarePortProvider
{
    private const double DefaultHitRadius = 20.0;
    private static readonly INodeShape DefaultShape = new RectangleShape();

    private readonly List<Port> _ports = new();
    private readonly double _hitRadiusSq;
    private readonly bool _layoutAware;
    private double _lastWidth;
    private double _lastHeight;
    private INodeShape _lastShape = DefaultShape;

    public IReadOnlyList<Port> Ports { get; }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;
    public event Action? LayoutInvalidated;

    public FixedPortProvider(double hitRadius = DefaultHitRadius, bool layoutAware = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitRadius);
        _hitRadiusSq = hitRadius * hitRadius;
        _layoutAware = layoutAware;
        Ports = _ports.AsReadOnly();
    }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius, bool layoutAware = false)
        : this(hitRadius, layoutAware)
    {
        ArgumentNullException.ThrowIfNull(ports);
        _ports.AddRange(ports);
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

    public void UpdateLayout(double width, double height, INodeShape? shape)
    {
        _lastWidth = width;
        _lastHeight = height;
        _lastShape = shape ?? DefaultShape;

        if (!_layoutAware) return;

        foreach (var port in _ports)
        {
            // Convert current top-left-relative position to center-relative
            var centerRel = new Point(port.Position.X - _lastWidth / 2.0, port.Position.Y - _lastHeight / 2.0);
            var boundary = _lastShape.GetNearestBoundaryPoint(centerRel, _lastWidth, _lastHeight);
            // Convert back to top-left-relative
            port.Position = new Point(boundary.X + _lastWidth / 2.0, boundary.Y + _lastHeight / 2.0);
        }

        LayoutInvalidated?.Invoke();
    }
}
```

- [ ] **Step 4: Update existing FixedPortProviderTests to use new signature**

Update all `ResolvePort(point)` calls to `ResolvePort(point, preview: true)`. Update constructor calls where needed (parameterless constructor now available).

- [ ] **Step 5: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~FixedPortProviderTests"`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/FixedPortProvider.cs tests/NodiumGraph.Tests/FixedPortProviderTests.cs
git commit -m "Rewrite FixedPortProvider: AddPort/RemovePort, events, preview param, layout-aware"
```

---

### Task 5: Rewrite DynamicPortProvider with new interface and cleanup

**Files:**
- Modify: `src/NodiumGraph/Model/DynamicPortProvider.cs`
- Modify: `tests/NodiumGraph.Tests/DynamicPortProviderTests.cs`

- [ ] **Step 1: Write failing tests for CancelResolve and pruning**

Add to `tests/NodiumGraph.Tests/DynamicPortProviderTests.cs`:

```csharp
[Fact]
public void CancelResolve_removes_last_created_port()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node);

    provider.ResolvePort(new Point(110, 25), preview: false);
    Assert.Single(provider.Ports);

    provider.CancelResolve();
    Assert.Empty(provider.Ports);
}

[Fact]
public void CancelResolve_fires_PortRemoved()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node);

    provider.ResolvePort(new Point(110, 25), preview: false);

    Port? removed = null;
    provider.PortRemoved += p => removed = p;

    provider.CancelResolve();
    Assert.NotNull(removed);
}

[Fact]
public void Preview_true_does_not_create_port()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node);

    var port = provider.ResolvePort(new Point(110, 25), preview: true);

    Assert.Null(port); // no existing ports to find
    Assert.Empty(provider.Ports);
}

[Fact]
public void Preview_true_returns_existing_port_near_position()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node);

    // Create a port first
    var created = provider.ResolvePort(new Point(110, 25), preview: false);

    // Preview should find it
    var found = provider.ResolvePort(new Point(112, 26), preview: true);
    Assert.Same(created, found);
}

[Fact]
public void ResolvePort_commit_fires_PortAdded()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node);

    Port? added = null;
    provider.PortAdded += p => added = p;

    provider.ResolvePort(new Point(110, 25), preview: false);
    Assert.NotNull(added);
}

[Fact]
public void PruneUnconnected_removes_ports_with_no_connections()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node);

    provider.ResolvePort(new Point(110, 25), preview: false);
    Assert.Single(provider.Ports);

    var graph = new Graph();
    graph.AddNode(node);
    node.PortProvider = provider;

    provider.PruneUnconnected(graph);
    Assert.Empty(provider.Ports);
}

[Fact]
public void NotifyDisconnected_removes_port_when_auto_prune_enabled()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node) { AutoPruneOnDisconnect = true };

    var port = provider.ResolvePort(new Point(110, 25), preview: false)!;

    var graph = new Graph();
    graph.AddNode(node);
    node.PortProvider = provider;

    provider.NotifyDisconnected(port, graph);
    Assert.Empty(provider.Ports);
}

[Fact]
public void NotifyDisconnected_keeps_port_when_auto_prune_disabled()
{
    var node = new Node { X = 0, Y = 0 };
    node.Width = 100;
    node.Height = 50;
    var provider = new DynamicPortProvider(node) { AutoPruneOnDisconnect = false };

    var port = provider.ResolvePort(new Point(110, 25), preview: false)!;

    var graph = new Graph();
    graph.AddNode(node);
    node.PortProvider = provider;

    provider.NotifyDisconnected(port, graph);
    Assert.Single(provider.Ports); // kept
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~DynamicPortProviderTests" --no-build 2>&1 | head -20`
Expected: FAIL — new methods/params don't exist

- [ ] **Step 3: Implement updated DynamicPortProvider**

```csharp
// src/NodiumGraph/Model/DynamicPortProvider.cs
using Avalonia;

namespace NodiumGraph.Model;

public class DynamicPortProvider : IPortProvider
{
    private const double DefaultReuseThreshold = 15.0;
    private const double DefaultMaxDistance = 50.0;
    private static readonly INodeShape DefaultShape = new RectangleShape();

    private readonly Node _owner;
    private readonly List<Port> _ports = new();
    private readonly double _reuseThresholdSq;
    private readonly double _maxDistanceSq;
    private Port? _lastCreated;

    public IReadOnlyList<Port> Ports { get; }
    public bool AutoPruneOnDisconnect { get; set; }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;

    public DynamicPortProvider(Node owner, double reuseThreshold = DefaultReuseThreshold, double maxDistance = DefaultMaxDistance)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reuseThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDistance);
        _owner = owner;
        _reuseThresholdSq = reuseThreshold * reuseThreshold;
        _maxDistanceSq = maxDistance * maxDistance;
        Ports = _ports.AsReadOnly();
    }

    public Port? ResolvePort(Point position, bool preview)
    {
        if (_owner.Width <= 0 || _owner.Height <= 0)
            return null;

        var shape = _owner.Shape ?? DefaultShape;
        var boundary = FindNearestBoundaryPoint(position, shape);
        if (boundary is null)
            return null;

        // Check for existing port to reuse
        foreach (var existing in _ports)
        {
            var abs = existing.AbsolutePosition;
            var dx = abs.X - boundary.Value.X;
            var dy = abs.Y - boundary.Value.Y;
            if (dx * dx + dy * dy < _reuseThresholdSq)
            {
                // Reusing existing port — no new port was created, clear last-created tracking
                if (!preview) _lastCreated = null;
                return existing;
            }
        }

        if (preview) return null;

        // Create new port
        var relative = new Point(boundary.Value.X - _owner.X, boundary.Value.Y - _owner.Y);
        var port = new Port(_owner, relative);
        _ports.Add(port);
        _lastCreated = port;
        PortAdded?.Invoke(port);
        return port;
    }

    public void CancelResolve()
    {
        if (_lastCreated == null) return;
        if (_ports.Remove(_lastCreated))
        {
            _lastCreated.Detach();
            PortRemoved?.Invoke(_lastCreated);
        }
        _lastCreated = null;
    }

    public void NotifyDisconnected(Port port, Graph graph)
    {
        if (!AutoPruneOnDisconnect) return;
        if (!_ports.Contains(port)) return;

        var hasConnections = false;
        foreach (var conn in graph.Connections)
        {
            if (conn.SourcePort == port || conn.TargetPort == port)
            {
                hasConnections = true;
                break;
            }
        }

        if (!hasConnections)
        {
            _ports.Remove(port);
            port.Detach();
            PortRemoved?.Invoke(port);
        }
    }

    public void PruneUnconnected(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        for (var i = _ports.Count - 1; i >= 0; i--)
        {
            var port = _ports[i];
            var connected = false;
            foreach (var conn in graph.Connections)
            {
                if (conn.SourcePort == port || conn.TargetPort == port)
                {
                    connected = true;
                    break;
                }
            }
            if (!connected)
            {
                _ports.RemoveAt(i);
                port.Detach();
                PortRemoved?.Invoke(port);
            }
        }
    }

    private Point? FindNearestBoundaryPoint(Point position, INodeShape shape)
    {
        // Convert world position to center-relative
        var centerX = _owner.X + _owner.Width / 2.0;
        var centerY = _owner.Y + _owner.Height / 2.0;
        var centerRel = new Point(position.X - centerX, position.Y - centerY);

        var boundaryCenter = shape.GetNearestBoundaryPoint(centerRel, _owner.Width, _owner.Height);

        // Convert back to world
        var worldBoundary = new Point(boundaryCenter.X + centerX, boundaryCenter.Y + centerY);

        // Check max distance
        var dx = position.X - worldBoundary.X;
        var dy = position.Y - worldBoundary.Y;
        if (dx * dx + dy * dy > _maxDistanceSq)
            return null;

        return worldBoundary;
    }
}
```

- [ ] **Step 4: Update existing DynamicPortProviderTests to use new signature**

Update all `ResolvePort(point)` calls to `ResolvePort(point, preview: false)`.

- [ ] **Step 5: Run all tests**

Run: `dotnet test --filter "FullyQualifiedName~DynamicPortProviderTests"`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add src/NodiumGraph/Model/DynamicPortProvider.cs tests/NodiumGraph.Tests/DynamicPortProviderTests.cs
git commit -m "Rewrite DynamicPortProvider: INodeShape, CancelResolve, AutoPrune, events"
```

---

### Task 6: Ensure Node.PortProvider fires PropertyChanged

**Files:**
- Modify: `src/NodiumGraph/Model/Node.cs`
- Modify: `tests/NodiumGraph.Tests/PortTests.cs` (or a new NodeTests.cs if one exists)

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void Setting_PortProvider_fires_PropertyChanged()
{
    var node = new Node();
    var changedProps = new List<string?>();
    node.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

    node.PortProvider = new FixedPortProvider();

    Assert.Contains(nameof(Node.PortProvider), changedProps);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "Setting_PortProvider_fires_PropertyChanged"`
Expected: FAIL — PortProvider is auto-property, no PropertyChanged

- [ ] **Step 3: Convert PortProvider to full property with backing field and SetField**

In `src/NodiumGraph/Model/Node.cs`, change:

```csharp
// Replace the auto-property:
//   public IPortProvider? PortProvider { get; set; }
// With:
private IPortProvider? _portProvider;

public IPortProvider? PortProvider
{
    get => _portProvider;
    set => SetField(ref _portProvider, value);
}
```

- [ ] **Step 4: Run test**

Run: `dotnet test --filter "Setting_PortProvider_fires_PropertyChanged"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/NodiumGraph/Model/Node.cs tests/
git commit -m "Make Node.PortProvider fire PropertyChanged for canvas resubscription"
```

---

### Task 7: Update NodiumGraphCanvas — unify hit-test, rollback, and event wiring

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`

This is the largest task. No new tests (canvas interactions require Avalonia headless platform).

- [ ] **Step 1: Replace HitTestPort with unified ResolvePort call**

In `NodiumGraphCanvas.cs`, replace the `HitTestPort` method (lines 342-377) with:

```csharp
internal Port? ResolvePort(Point screenPosition, bool preview)
{
    if (Graph == null) return null;

    var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
    var worldPosition = transform.ScreenToWorld(screenPosition);

    foreach (var node in Graph.Nodes)
    {
        if (node.IsCollapsed) continue;
        if (node.PortProvider == null) continue;
        var port = node.PortProvider.ResolvePort(worldPosition, preview);
        if (port != null) return port;
    }

    return null;
}
```

Delete `ResolvePortForConnection` (lines 379-394).

- [ ] **Step 2: Add _commitProvider tracking**

Add field:
```csharp
private IPortProvider? _commitProvider;
```

- [ ] **Step 3: Update OnPointerMoved for preview**

Replace `HitTestPort(_connectionPreviewEnd)` call (line 680) with:
```csharp
var targetPort = ResolvePort(_connectionPreviewEnd, preview: true);
```

- [ ] **Step 4: Update OnPointerReleased for commit with rollback**

Replace the connection drawing release block (lines 824-850) with:

```csharp
if (_isDrawingConnection && _connectionSourcePort != null)
{
    var position = e.GetPosition(this);

    // Commit-time resolve — may create a dynamic port
    _commitProvider = null;
    Port? targetPort = null;
    if (Graph != null)
    {
        var transform = new ViewportTransform(ViewportZoom, ViewportOffset);
        var worldPosition = transform.ScreenToWorld(position);

        foreach (var node in Graph.Nodes)
        {
            if (node.IsCollapsed) continue;
            if (node.PortProvider == null) continue;
            var port = node.PortProvider.ResolvePort(worldPosition, preview: false);
            if (port != null)
            {
                targetPort = port;
                _commitProvider = node.PortProvider;
                break;
            }
        }
    }

    var connected = false;
    if (targetPort != null && targetPort != _connectionSourcePort)
    {
        var canConnect = ConnectionValidator?.CanConnect(_connectionSourcePort, targetPort) ?? true;
        if (canConnect)
        {
            var result = ConnectionHandler?.OnConnectionRequested(_connectionSourcePort, targetPort);
            if (result is { IsSuccess: true })
                connected = true;
        }
    }

    // Rollback on failed commit
    if (!connected)
        _commitProvider?.CancelResolve();

    _commitProvider = null;
    _isDrawingConnection = false;
    _connectionSourcePort = null;
    _connectionTargetPort = null;
    InvalidateVisual();
    e.Handled = true;
    return;
}
```

- [ ] **Step 5: Update OnPointerPressed to use ResolvePort**

Replace `HitTestPort(position)` call (line 614) with:
```csharp
var hitPort = ResolvePort(position, preview: true);
```

- [ ] **Step 6: Add port/style event subscription wiring**

Add these fields:

```csharp
private readonly Dictionary<IPortProvider, Action<Port>> _providerAddedHandlers = new();
private readonly Dictionary<IPortProvider, Action<Port>> _providerRemovedHandlers = new();
```

Add helper methods:

```csharp
private void AttachProvider(IPortProvider provider)
{
    // Subscribe to existing ports
    foreach (var port in provider.Ports)
        SubscribeToPort(port);

    // Subscribe to future membership changes
    Action<Port> onAdded = p => { SubscribeToPort(p); InvalidateVisual(); };
    Action<Port> onRemoved = p => { UnsubscribeFromPort(p); InvalidateVisual(); };
    provider.PortAdded += onAdded;
    provider.PortRemoved += onRemoved;
    _providerAddedHandlers[provider] = onAdded;
    _providerRemovedHandlers[provider] = onRemoved;
}

private void DetachProvider(IPortProvider provider)
{
    foreach (var port in provider.Ports)
        UnsubscribeFromPort(port);

    if (_providerAddedHandlers.TryGetValue(provider, out var onAdded))
        provider.PortAdded -= onAdded;
    if (_providerRemovedHandlers.TryGetValue(provider, out var onRemoved))
        provider.PortRemoved -= onRemoved;
    _providerAddedHandlers.Remove(provider);
    _providerRemovedHandlers.Remove(provider);
}

private void SubscribeToPort(Port port)
{
    port.PropertyChanged += OnPortPropertyChanged;
    if (port.Style != null)
        port.Style.PropertyChanged += OnPortStylePropertyChanged;
}

private void UnsubscribeFromPort(Port port)
{
    port.PropertyChanged -= OnPortPropertyChanged;
    if (port.Style != null)
        port.Style.PropertyChanged -= OnPortStylePropertyChanged;
}

private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (sender is Port port && e.PropertyName == nameof(Port.Style))
    {
        // Re-subscribe to new style instance (old one already unsubscribed via setter)
        // Note: we need to track old style — simpler to just invalidate
        InvalidateVisual();
    }
    else if (e.PropertyName is nameof(Port.AbsolutePosition) or nameof(Port.Label))
    {
        InvalidateVisual();
    }
}

private void OnPortStylePropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    InvalidateVisual();
}
```

Wire `Node.PropertyChanged` for PortProvider replacement in the node add/remove handlers:

```csharp
// In node-added handling:
node.PropertyChanged += OnNodePropertyChanged;
if (node.PortProvider != null)
    AttachProvider(node.PortProvider);

// In node-removed handling:
node.PropertyChanged -= OnNodePropertyChanged;
if (node.PortProvider != null)
    DetachProvider(node.PortProvider);

private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (sender is not Node node) return;
    if (e.PropertyName == nameof(Node.PortProvider))
    {
        // Can't get old value from event — detach all tracked providers for this node
        // and re-attach the new one. Use a node->provider mapping if needed.
        // Simpler: track per-node provider mapping.
        if (node.PortProvider != null)
            AttachProvider(node.PortProvider);
        InvalidateVisual();
    }
}
```

On initial graph attach, enumerate all existing nodes and call `AttachProvider` for each.

- [ ] **Step 7: Add auto-prune wiring**

In `OnConnectionsCollectionChanged`, when a connection is removed:

```csharp
if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
{
    foreach (Connection conn in e.OldItems)
    {
        NotifyProviderOfDisconnect(conn.SourcePort);
        NotifyProviderOfDisconnect(conn.TargetPort);
    }
    InvalidateVisual();
}

private void NotifyProviderOfDisconnect(Port port)
{
    if (Graph == null) return;
    if (port.Owner.PortProvider is DynamicPortProvider dynamicProvider)
        dynamicProvider.NotifyDisconnected(port, Graph);
}
```

- [ ] **Step 8: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 9: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "Unify canvas hit-test, add commit rollback, wire port/style events"
```

---

### Task 8: Update CanvasOverlay for label placement

**Files:**
- Modify: `src/NodiumGraph/Controls/CanvasOverlay.cs`

- [ ] **Step 1: Replace angle-based label placement with position-based heuristic**

In `CanvasOverlay.cs` line 180, replace:
```csharp
var placement = port.LabelPlacement ?? GetAutoPlacement(port.Angle);
```
with:
```csharp
var placement = port.Style?.LabelPlacement ?? GetAutoPlacement(port, node);
```

Update `GetAutoPlacement` to use port position relative to node center:
```csharp
private static PortLabelPlacement GetAutoPlacement(Port port, Node node)
{
    var centerX = node.Width / 2.0;
    var relX = port.Position.X - centerX;
    return relX >= 0 ? PortLabelPlacement.Right : PortLabelPlacement.Left;
}
```

Remove the old angle-based `GetAutoPlacement` method.

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add src/NodiumGraph/Controls/CanvasOverlay.cs
git commit -m "Update CanvasOverlay: position-based label placement, remove angle dependency"
```

---

### Task 9: Update sample app to use new API

**Files:**
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml.cs`

- [ ] **Step 1: Replace all AnglePortProvider usage with FixedPortProvider**

Convert each node from angle-based to position-based ports. Example for Input Source:

```csharp
// Before:
var inputOut = new Port(inputNode, "out", PortFlow.Output, default) { Angle = 90, Label = "out" };
var inputProvider = new AnglePortProvider();
inputProvider.AddPort(inputOut);

// After:
var inputProvider = new FixedPortProvider(layoutAware: true);
var inputOut = new Port(inputNode, "out", PortFlow.Output, new Point(0, 0)) { Label = "out" };
inputProvider.AddPort(inputOut);
```

Port positions will be approximate — with `layoutAware: true`, they'll snap to the boundary on first layout.

- [ ] **Step 2: Build and run**

Run: `dotnet build && dotnet run --project samples/NodiumGraph.Sample`
Expected: App launches, nodes display with ports, connections work

- [ ] **Step 3: Commit**

```bash
git add samples/NodiumGraph.Sample/MainWindow.axaml.cs
git commit -m "Update sample app: replace AnglePortProvider with FixedPortProvider"
```

---

### Task 10: Run full test suite and verify clean build

- [ ] **Step 1: Run full test suite**

Run: `dotnet test`
Expected: All tests PASS, zero warnings related to port system

- [ ] **Step 2: Build in Release mode**

Run: `dotnet build -c Release`
Expected: Clean build, no warnings

- [ ] **Step 3: Run sample app and verify**

Run: `dotnet run --project samples/NodiumGraph.Sample`
Verify: nodes, ports, connections, drag, connect, disconnect all work correctly

- [ ] **Step 4: Final commit if any cleanup needed**

```bash
git add -A
git commit -m "Port system improvements: final cleanup"
```
