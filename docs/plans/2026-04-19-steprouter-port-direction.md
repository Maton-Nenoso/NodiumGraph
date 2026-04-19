# StepRouter Port-Direction Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Teach StepRouter to route based on each port's emission direction (which edge of the owner node the port sits on), mirroring the awareness BezierRouter gained on 2026-04-14.

**Architecture:** Extract BezierRouter's private `GetEmissionDirection` into a shared `PortEmissionDirection` helper. Rewrite `StepRouter.Route` to classify each port as horizontal- or vertical-emitting, then pick a leg pattern per combination (H+H → midX H–V–H, V+V → midY V–H–V, mixed → L-bend with corner matching both emissions). Aligned-axis shortcuts stay but are gated by emission matching; degenerate bends collapse via consecutive-duplicate dedup.

**Tech Stack:** C# / .NET 10, Avalonia 12, xUnit v3.

**Design reference:** `docs/plans/2026-04-19-steprouter-port-direction-design.md`

---

### Task 0: Extract PortEmissionDirection helper

Refactor with no behavior change. Existing BezierRouter tests keep passing.

**Files:**
- Create: `src/NodiumGraph/Interactions/PortEmissionDirection.cs`
- Modify: `src/NodiumGraph/Interactions/BezierRouter.cs` (remove private `GetEmissionDirection`, call shared helper)

**Step 1: Create helper**

`src/NodiumGraph/Interactions/PortEmissionDirection.cs`:

```csharp
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Resolves the outward emission direction of a port from which edge of its owner
/// node it sits closest to. Returns one of the four cardinal unit vectors.
/// </summary>
internal static class PortEmissionDirection
{
    public static Vector Resolve(Port port)
    {
        var owner = port.Owner;
        var px = port.Position.X;
        var py = port.Position.Y;

        var leftDist = px;
        var rightDist = owner.Width - px;
        var topDist = py;
        var bottomDist = owner.Height - py;

        // Pick the axis with the smallest edge distance, then the nearer side on that axis.
        // Negative distances (port declared outside its owner) naturally win because they're
        // the smallest — the emission vector then points toward the side the port is beyond.
        // Ties break horizontal-first to preserve the default for corner / interior / zero-size ports.
        var minHorizontal = Math.Min(leftDist, rightDist);
        var minVertical = Math.Min(topDist, bottomDist);

        if (minHorizontal <= minVertical)
        {
            return leftDist <= rightDist ? new Vector(-1, 0) : new Vector(1, 0);
        }

        return topDist <= bottomDist ? new Vector(0, -1) : new Vector(0, 1);
    }
}
```

**Step 2: Adopt in BezierRouter**

In `src/NodiumGraph/Interactions/BezierRouter.cs`:

Replace the two call sites (currently `GetEmissionDirection(source)` and `GetEmissionDirection(target)`) with `PortEmissionDirection.Resolve(source)` / `PortEmissionDirection.Resolve(target)`, and delete the private `GetEmissionDirection` method (currently lines 45–70).

**Step 3: Run tests — verify green**

Run: `dotnet test`
Expected: all tests pass (pure refactor — logic is byte-identical).

**Step 4: Commit**

```bash
git add src/NodiumGraph/Interactions/PortEmissionDirection.cs src/NodiumGraph/Interactions/BezierRouter.cs
git commit -m "refactor(routers): extract PortEmissionDirection helper"
```

---

### Task 1: Pin H+H midX path with explicit tests

H+H is today's behavior — this task adds named tests that pin it explicitly so the subsequent refactor can't regress it silently.

**Files:**
- Modify: `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 1: Write the tests**

Append to `tests/NodiumGraph.Tests/StepRouterTests.cs` (inside the `StepRouterTests` class):

```csharp
[Fact]
public void Route_both_ports_horizontal_returns_midX_HVH()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 100, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));  // right edge → H
    var target = new Port(nodeB, new Point(0, 25));    // left edge  → H

    var points = router.Route(source, target);

    Assert.Equal(4, points.Count);
    Assert.Equal(new Point(100, 25), points[0]);
    Assert.Equal(new Point(200, 25), points[1]);   // midX, start.Y
    Assert.Equal(new Point(200, 125), points[2]);  // midX, end.Y
    Assert.Equal(new Point(300, 125), points[3]);
}

[Fact]
public void Route_both_ports_horizontal_aligned_row_returns_straight_line()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));  // right edge → H
    var target = new Port(nodeB, new Point(0, 25));    // left edge  → H

    var points = router.Route(source, target);

    Assert.Equal(2, points.Count);
    Assert.Equal(new Point(100, 25), points[0]);
    Assert.Equal(new Point(300, 25), points[1]);
}
```

**Step 2: Run tests — verify green**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: both new tests pass (H+H matches current implementation).

**Step 3: Commit**

```bash
git add tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "test(StepRouter): pin H+H midX path and aligned-row shortcut"
```

---

### Task 2: StepRouter V+V midY path (TDD)

**Files:**
- Modify: `tests/NodiumGraph.Tests/StepRouterTests.cs`
- Modify: `src/NodiumGraph/Interactions/StepRouter.cs`

**Step 1: Write the failing tests**

Append to `StepRouterTests`:

```csharp
[Fact]
public void Route_both_ports_vertical_returns_midY_VHV()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 200, Y = 300, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V (down)
    var target = new Port(nodeB, new Point(50, 0));     // top edge    → V (up)

    var points = router.Route(source, target);

    Assert.Equal(4, points.Count);
    Assert.Equal(new Point(50, 50), points[0]);
    Assert.Equal(new Point(50, 175), points[1]);    // start.X, midY
    Assert.Equal(new Point(250, 175), points[2]);   // end.X, midY
    Assert.Equal(new Point(250, 300), points[3]);
}

[Fact]
public void Route_both_ports_vertical_aligned_column_returns_straight_line()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 0, Y = 300, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V
    var target = new Port(nodeB, new Point(50, 0));     // top edge    → V

    var points = router.Route(source, target);

    Assert.Equal(2, points.Count);
    Assert.Equal(new Point(50, 50), points[0]);
    Assert.Equal(new Point(50, 300), points[1]);
}
```

**Step 2: Run tests — verify they fail**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: both new tests FAIL. Current StepRouter produces midX H-V-H regardless of emission.

**Step 3: Rewrite StepRouter**

Replace the body of `src/NodiumGraph/Interactions/StepRouter.cs` with:

```csharp
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Routes a connection as an orthogonal (Manhattan) path. Leg orientations are chosen
/// from each port's emission direction (derived from which edge of the owner node the
/// port sits on). Two horizontally-emitting ports produce an H–V–H path through midX;
/// two vertically-emitting ports produce a V–H–V path through midY; a mixed pair
/// produces a single L-bend whose corner matches both emissions.
/// </summary>
public class StepRouter : IConnectionRouter
{
    private const double Epsilon = 0.001;

    public RouteKind RouteKind => RouteKind.Polyline;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var sourceDir = PortEmissionDirection.Resolve(source);
        var targetDir = PortEmissionDirection.Resolve(target);

        var sourceHorizontal = Math.Abs(sourceDir.X) > 0.5;
        var targetHorizontal = Math.Abs(targetDir.X) > 0.5;

        Point[] raw;

        if (sourceHorizontal && targetHorizontal)
        {
            if (Math.Abs(start.Y - end.Y) < Epsilon)
                return [start, end];

            var midX = (start.X + end.X) / 2;
            raw = [start, new Point(midX, start.Y), new Point(midX, end.Y), end];
        }
        else if (!sourceHorizontal && !targetHorizontal)
        {
            if (Math.Abs(start.X - end.X) < Epsilon)
                return [start, end];

            var midY = (start.Y + end.Y) / 2;
            raw = [start, new Point(start.X, midY), new Point(end.X, midY), end];
        }
        else if (sourceHorizontal)
        {
            raw = [start, new Point(end.X, start.Y), end];
        }
        else
        {
            raw = [start, new Point(start.X, end.Y), end];
        }

        return Dedup(raw);
    }

    private static IReadOnlyList<Point> Dedup(Point[] points)
    {
        var result = new List<Point>(points.Length) { points[0] };
        for (int i = 1; i < points.Length; i++)
        {
            var prev = result[^1];
            if (Math.Abs(prev.X - points[i].X) < Epsilon && Math.Abs(prev.Y - points[i].Y) < Epsilon)
                continue;
            result.Add(points[i]);
        }
        return result;
    }
}
```

**Step 4: Run tests — verify they pass**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: V+V tests PASS. All H+H tests (Task 1 + existing `Route_horizontal_aligned_returns_straight_horizontal`) still PASS.

**Step 5: Run full suite**

Run: `dotnet test`
Expected: all green. (The existing `Route_returns_orthogonal_segments` test uses ports on zero-size nodes which resolve to mixed emission; the L-bend it produces is still orthogonal, satisfying that test's assertion. The existing `Route_vertically_aligned_returns_two_points` test uses ports at (0,0) on zero-size nodes → both H; X-aligned H+H falls into the midX branch where midX equals both X values, Dedup collapses to 2 points.)

**Step 6: Commit**

```bash
git add tests/NodiumGraph.Tests/StepRouterTests.cs src/NodiumGraph/Interactions/StepRouter.cs
git commit -m "feat(StepRouter): add emission-aware routing for V+V and mixed pairs"
```

---

### Task 3: Pin H+V L-bend

H+V is already functional after Task 2. This task adds explicit pinning tests.

**Files:**
- Modify: `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 1: Write the tests**

Append to `StepRouterTests`:

```csharp
[Fact]
public void Route_source_horizontal_target_vertical_bends_at_end_X_start_Y()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 200, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));   // right edge → H
    var target = new Port(nodeB, new Point(50, 0));     // top edge   → V (up)

    var points = router.Route(source, target);

    Assert.Equal(3, points.Count);
    Assert.Equal(new Point(100, 25), points[0]);
    Assert.Equal(new Point(350, 25), points[1]);   // end.X, start.Y
    Assert.Equal(new Point(350, 200), points[2]);
}
```

**Step 2: Run**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: green.

**Step 3: Commit**

```bash
git add tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "test(StepRouter): pin H+V L-bend corner"
```

---

### Task 4: Pin V+H L-bend

Mirror of Task 3.

**Files:**
- Modify: `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 1: Write the tests**

Append to `StepRouterTests`:

```csharp
[Fact]
public void Route_source_vertical_target_horizontal_bends_at_start_X_end_Y()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 200, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V (down)
    var target = new Port(nodeB, new Point(0, 25));     // left edge   → H

    var points = router.Route(source, target);

    Assert.Equal(3, points.Count);
    Assert.Equal(new Point(50, 50), points[0]);
    Assert.Equal(new Point(50, 225), points[1]);   // start.X, end.Y
    Assert.Equal(new Point(300, 225), points[2]);
}
```

**Step 2: Run**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: green.

**Step 3: Commit**

```bash
git add tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "test(StepRouter): pin V+H L-bend corner"
```

---

### Task 5: Pin degenerate-bend collapse

Verify the Dedup path handles mixed-emission on aligned row/column without leaking zero-length segments.

**Files:**
- Modify: `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 1: Write the tests**

Append to `StepRouterTests`:

```csharp
[Fact]
public void Route_mixed_emission_on_aligned_row_collapses_to_line()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));   // right edge → H
    var target = new Port(nodeB, new Point(50, 0));     // top edge   → V
    // Absolute Y: source = 25, target = 0 → not strictly aligned.
    // Force alignment: move target's port to y=25 on its node (mid-height).
    // Actually easier: place target so start.Y == end.Y by choosing nodeB.Y = 25.

    // Re-declare with aligned absolute Y:
    var nodeBAligned = new Node { X = 300, Y = 25, Width = 100, Height = 50 };
    var targetAligned = new Port(nodeBAligned, new Point(50, 0));  // top edge → V
    // source AbsolutePosition = (100, 25), target AbsolutePosition = (350, 25)

    var points = router.Route(source, targetAligned);

    // H+V L-bend: [start, (end.X, start.Y), end] = [(100,25), (350,25), (350,25)]
    // Dedup collapses consecutive duplicate → [(100,25), (350,25)]
    Assert.Equal(2, points.Count);
    Assert.Equal(new Point(100, 25), points[0]);
    Assert.Equal(new Point(350, 25), points[1]);
}

[Fact]
public void Route_mixed_emission_on_aligned_column_collapses_to_line()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 50, Y = 300, Width = 100, Height = 50 };
    // source port: bottom edge → V; absolute = (50, 50)
    var source = new Port(nodeA, new Point(50, 50));
    // target port: left edge → H; absolute = (50, 325) — same X as source
    var target = new Port(nodeB, new Point(0, 25));

    var points = router.Route(source, target);

    // V+H L-bend: [start, (start.X, end.Y), end] = [(50,50), (50,325), (50,325)]
    // Dedup → [(50,50), (50,325)]
    Assert.Equal(2, points.Count);
    Assert.Equal(new Point(50, 50), points[0]);
    Assert.Equal(new Point(50, 325), points[1]);
}
```

**Step 2: Run**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: green.

**Step 3: Commit**

```bash
git add tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "test(StepRouter): pin degenerate-bend collapse on aligned axes"
```

---

### Task 6: Pin partner-behind degradation

Document the accepted scope cutoff: when a partner sits behind the emission direction, the leading leg may contradict emission. These tests lock in the current (degraded) behavior so a future regression isn't mistaken for this documented limit.

**Files:**
- Modify: `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 1: Write the tests**

Append to `StepRouterTests`:

```csharp
[Fact]
public void Route_both_horizontal_target_behind_source_still_uses_midX()
{
    // Both ports emit → (right edge). Target sits LEFT of source (behind source's emission).
    // midX H-V-H is geometrically valid but the first leg goes LEFT, contradicting source emission.
    // This is the documented "partner behind" scope limit — no detour segments.
    var router = new StepRouter();
    var nodeA = new Node { X = 400, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 0, Y = 200, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));   // right edge → H
    var target = new Port(nodeB, new Point(100, 25));   // right edge → H

    var points = router.Route(source, target);

    Assert.Equal(4, points.Count);
    Assert.Equal(new Point(500, 25), points[0]);
    Assert.Equal(new Point(350, 25), points[1]);    // midX = (500 + 200) / 2 = 350
    Assert.Equal(new Point(350, 225), points[2]);
    Assert.Equal(new Point(100, 225), points[3]);
}

[Fact]
public void Route_both_vertical_target_behind_source_still_uses_midY()
{
    var router = new StepRouter();
    var nodeA = new Node { X = 0, Y = 400, Width = 100, Height = 50 };
    var nodeB = new Node { X = 200, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(50, 50));    // bottom edge → V (down)
    var target = new Port(nodeB, new Point(50, 50));    // bottom edge → V (down)

    var points = router.Route(source, target);

    Assert.Equal(4, points.Count);
    Assert.Equal(new Point(50, 450), points[0]);
    Assert.Equal(new Point(50, 250), points[1]);    // midY = (450 + 50) / 2 = 250
    Assert.Equal(new Point(250, 250), points[2]);
    Assert.Equal(new Point(250, 50), points[3]);
}
```

**Step 2: Run**

Run: `dotnet test --filter FullyQualifiedName~StepRouterTests`
Expected: green.

**Step 3: Commit**

```bash
git add tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "test(StepRouter): pin partner-behind midpoint path (documented limit)"
```

---

### Task 7: Full suite + index refresh

**Step 1: Run the full suite**

Run: `dotnet test`
Expected: all tests pass.

**Step 2: Refresh the jcodemunch index**

Call `register_edit` with the three changed/created paths so future queries pick up the new symbols:

- `src/NodiumGraph/Interactions/PortEmissionDirection.cs`
- `src/NodiumGraph/Interactions/BezierRouter.cs`
- `src/NodiumGraph/Interactions/StepRouter.cs`
- `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 3: Final build**

Run: `dotnet build`
Expected: zero warnings, zero errors.

No commit — this task is verification-only.
