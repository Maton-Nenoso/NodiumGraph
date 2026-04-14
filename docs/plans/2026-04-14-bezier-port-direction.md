---
title: BezierRouter Port-Direction Awareness — Implementation Plan
tags: [plan]
status: active
created: 2026-04-14
updated: 2026-04-14
---

# BezierRouter Port-Direction Awareness — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Make `BezierRouter` derive emission direction from where each port sits on its owner node, so top/bottom-edge ports produce correctly oriented curves instead of shooting sideways.

**Architecture:** `BezierRouter` gains a private static `GetEmissionDirection(Port)` helper that classifies each port geometrically against `Owner.Width` / `Owner.Height`. `Route()` projects the source→target delta onto each port's outward unit vector to compute per-end control-point offsets, then pushes `cp1` / `cp2` along the port's direction. No public API change. No change to `Port`, `IPortProvider`, `StepRouter`, or `StraightRouter`. Bounds code is already direction-agnostic (convex-hull).

**Tech Stack:** C# 13, .NET 10, Avalonia 12, xUnit v3.

**Design doc:** [[2026-04-14-bezier-port-direction-design]]

---

## Pre-flight

Confirm the working tree is clean and on `main`:

```bash
git status
git log --oneline -1
```

Expected: clean tree, HEAD at `8c6c8db docs: add BezierRouter port-direction-aware design`.

---

### Task 0: Refactor existing BezierRouter tests to set Node size

Behavior-preserving refactor. The current `Route()` implementation ignores `Node.Width` / `Height`, so existing tests still pass against current code once setup is updated. This lands first so the later rewrite doesn't look like it's breaking tests.

**Files:**
- Modify: `tests/NodiumGraph.Tests/BezierRouterTests.cs` (replace the 4 existing tests' setup)

**Step 1: Update `Route_returns_four_points_for_bezier_curve` (line 10–24)**

Replace the test body with:

```csharp
[Fact]
public void Route_returns_four_points_for_bezier_curve()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));  // right edge
    var target = new Port(nodeB, new Point(0, 25));    // left edge

    var points = router.Route(source, target);

    Assert.Equal(4, points.Count);
    Assert.Equal(source.AbsolutePosition, points[0]);
    Assert.Equal(target.AbsolutePosition, points[3]);
}
```

**Step 2: Update `Control_points_are_horizontally_offset` (line 26–42)**

Same setup pattern. Assertions unchanged:

```csharp
[Fact]
public void Control_points_are_horizontally_offset()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));  // right edge
    var target = new Port(nodeB, new Point(0, 25));    // left edge

    var points = router.Route(source, target);

    Assert.Equal(points[0].Y, points[1].Y);
    Assert.Equal(points[3].Y, points[2].Y);
    Assert.True(points[1].X > points[0].X);
    Assert.True(points[2].X < points[3].X);
}
```

**Step 3: Update `Offset_scales_with_distance` (line 44–64)**

Ports on explicit edges; node-far repositioned so delta grows:

```csharp
[Fact]
public void Offset_scales_with_distance()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));  // right edge

    var nodeNear = new Node { X = 200, Y = 0, Width = 100, Height = 50 };
    var targetNear = new Port(nodeNear, new Point(0, 25));  // left edge

    var nodeFar = new Node { X = 600, Y = 0, Width = 100, Height = 50 };
    var targetFar = new Port(nodeFar, new Point(0, 25));    // left edge

    var nearPoints = router.Route(source, targetNear);
    var farPoints = router.Route(source, targetFar);

    var nearOffset = nearPoints[1].X - nearPoints[0].X;
    var farOffset = farPoints[1].X - farPoints[0].X;

    Assert.True(farOffset > nearOffset);
}
```

**Step 4: Update `Route_right_to_left_does_not_cross` (line 66–86)**

Source on left edge of right-hand node, target on right edge of left-hand node — ports facing each other under reversed layout:

```csharp
[Fact]
public void Route_right_to_left_does_not_cross()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 300, Y = 0, Width = 100, Height = 100 };
    var nodeB = new Node { X = 100, Y = 0, Width = 100, Height = 100 };
    var source = new Port(nodeA, new Point(0, 50));    // left edge of nodeA
    var target = new Port(nodeB, new Point(100, 50));  // right edge of nodeB

    var points = router.Route(source, target);

    var cp1 = points[1];
    var cp2 = points[2];

    Assert.True(cp1.X <= points[0].X,
        $"cp1.X ({cp1.X}) should be <= start.X ({points[0].X}) for right-to-left connection");
    Assert.True(cp2.X >= points[3].X,
        $"cp2.X ({cp2.X}) should be >= end.X ({points[3].X}) for right-to-left connection");
}
```

**Step 5: Run tests — all 4 must still pass against current `BezierRouter`**

```bash
dotnet test --filter "FullyQualifiedName~BezierRouterTests"
```

Expected: 4 passed, 0 failed. (If any fail here, the refactor introduced unintended changes — stop and diagnose.)

**Step 6: Commit**

```bash
git add tests/NodiumGraph.Tests/BezierRouterTests.cs
git commit -m "test(BezierRouter): set Node size on test fixtures"
```

---

### Task 1: Add failing test for vertical emission

Write the red test that motivates the whole change. It will fail against the current horizontal-only implementation.

**Files:**
- Modify: `tests/NodiumGraph.Tests/BezierRouterTests.cs` (append)

**Step 1: Add the failing test**

Append inside `BezierRouterTests` class:

```csharp
[Fact]
public void Route_with_bottom_to_top_ports_pushes_control_points_vertically()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 0, Y = 200, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(50, 50));   // bottom edge of nodeA
    var target = new Port(nodeB, new Point(50, 0));    // top edge of nodeB

    var points = router.Route(source, target);
    var start = points[0];
    var cp1 = points[1];
    var cp2 = points[2];
    var end = points[3];

    Assert.Equal(start.X, cp1.X);
    Assert.True(cp1.Y > start.Y,
        $"cp1.Y ({cp1.Y}) should be > start.Y ({start.Y}) for a downward-emitting source");
    Assert.Equal(end.X, cp2.X);
    Assert.True(cp2.Y < end.Y,
        $"cp2.Y ({cp2.Y}) should be < end.Y ({end.Y}) for an upward-emitting target");
}
```

**Step 2: Run it — must fail**

```bash
dotnet test --filter "FullyQualifiedName~BezierRouterTests.Route_with_bottom_to_top_ports_pushes_control_points_vertically"
```

Expected: FAIL. Current formula pushes cp1 / cp2 horizontally only, so `cp1.Y == start.Y` instead of `>`.

**Step 3: Commit the red**

```bash
git add tests/NodiumGraph.Tests/BezierRouterTests.cs
git commit -m "test(BezierRouter): red test for vertical port emission"
```

---

### Task 2: Rewrite `BezierRouter.Route` with direction-aware math

Replace the body of `Route()` and add the private classification helper. Existing constants (`MinOffset`, `ControlOffsetFactor`) are reused.

**Files:**
- Modify: `src/NodiumGraph/Interactions/BezierRouter.cs`

**Step 1: Update the XML doc comment**

Replace lines 6–9:

```csharp
/// <summary>
/// Routes a connection as a cubic bezier curve. Control points are pushed along each port's
/// outward emission direction, derived from which edge of the owner node the port sits on.
/// Returns 4 points: start, control point 1, control point 2, end.
/// </summary>
```

**Step 2: Replace `Route()` body and add `GetEmissionDirection`**

Replace lines 17–33 with:

```csharp
public IReadOnlyList<Point> Route(Port source, Port target)
{
    var start = source.AbsolutePosition;
    var end = target.AbsolutePosition;

    var sourceDir = GetEmissionDirection(source);
    var targetDir = GetEmissionDirection(target);

    var dx = end.X - start.X;
    var dy = end.Y - start.Y;

    var sourceReach = Math.Abs(dx * sourceDir.X + dy * sourceDir.Y);
    var targetReach = Math.Abs(dx * targetDir.X + dy * targetDir.Y);

    var sourceOffset = Math.Max(sourceReach * ControlOffsetFactor, MinOffset);
    var targetOffset = Math.Max(targetReach * ControlOffsetFactor, MinOffset);

    var cp1 = new Point(start.X + sourceDir.X * sourceOffset,
                        start.Y + sourceDir.Y * sourceOffset);
    var cp2 = new Point(end.X + targetDir.X * targetOffset,
                        end.Y + targetDir.Y * targetOffset);

    return [start, cp1, cp2, end];
}

private static Vector GetEmissionDirection(Port port)
{
    var owner = port.Owner;
    var px = port.Position.X;
    var py = port.Position.Y;

    var leftDist = px;
    var rightDist = owner.Width - px;
    var topDist = py;
    var bottomDist = owner.Height - py;

    // Smallest signed distance wins. Negative means the port is outside on that side —
    // correctly picked. Ties between horizontal and vertical break toward horizontal to
    // preserve today's behavior for corner / interior / zero-size ports.
    var minHorizontal = Math.Min(leftDist, rightDist);
    var minVertical = Math.Min(topDist, bottomDist);

    if (minHorizontal <= minVertical)
    {
        return leftDist <= rightDist ? new Vector(-1, 0) : new Vector(1, 0);
    }

    return topDist <= bottomDist ? new Vector(0, -1) : new Vector(0, 1);
}
```

**Step 3: Run the red test — must now pass**

```bash
dotnet test --filter "FullyQualifiedName~BezierRouterTests.Route_with_bottom_to_top_ports_pushes_control_points_vertically"
```

Expected: PASS.

**Step 4: Run all BezierRouter tests — existing 4 must still pass, new test passes**

```bash
dotnet test --filter "FullyQualifiedName~BezierRouterTests"
```

Expected: 5 passed, 0 failed.

**Step 5: Run the full test suite — catch any unexpected regressions**

```bash
dotnet test
```

Expected: everything green. Previous baseline was 395 tests; count is now 396.

**Step 6: Commit the green**

```bash
git add src/NodiumGraph/Interactions/BezierRouter.cs
git commit -m "feat(BezierRouter): derive emission direction from port placement"
```

---

### Task 3: Add remaining direction-aware tests

Four more tests covering same-Y arc, mixed horizontal/vertical emission, corner tie-break, and zero-size-node fallback.

**Files:**
- Modify: `tests/NodiumGraph.Tests/BezierRouterTests.cs`

**Step 1: Append four tests**

Inside `BezierRouterTests`:

```csharp
[Fact]
public void Route_with_top_ports_on_same_y_produces_arc()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 200, Y = 0, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(50, 0));  // top edge
    var target = new Port(nodeB, new Point(50, 0));  // top edge

    var points = router.Route(source, target);
    var start = points[0];
    var cp1 = points[1];
    var cp2 = points[2];
    var end = points[3];

    // Both ports emit upward; dy == 0 so reach clamps to MinOffset (30).
    Assert.Equal(start.Y - 30, cp1.Y);
    Assert.Equal(end.Y - 30, cp2.Y);
    Assert.Equal(start.X, cp1.X);
    Assert.Equal(end.X, cp2.X);
}

[Fact]
public void Route_with_mixed_horizontal_and_vertical_ports_pushes_independently()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 50 };
    var nodeB = new Node { X = 300, Y = 200, Width = 100, Height = 50 };
    var source = new Port(nodeA, new Point(100, 25));  // right edge → (+1, 0)
    var target = new Port(nodeB, new Point(50, 0));    // top edge   → (0, -1)

    var points = router.Route(source, target);
    var start = points[0];
    var cp1 = points[1];
    var cp2 = points[2];
    var end = points[3];

    // cp1 pushed horizontally only.
    Assert.Equal(start.Y, cp1.Y);
    Assert.True(cp1.X > start.X);

    // cp2 pushed vertically only.
    Assert.Equal(end.X, cp2.X);
    Assert.True(cp2.Y < end.Y);
}

[Fact]
public void Route_classifies_corner_port_as_horizontal()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0, Width = 100, Height = 100 };
    var nodeB = new Node { X = 300, Y = 300, Width = 100, Height = 100 };
    var source = new Port(nodeA, new Point(0, 0));  // top-left corner
    var target = new Port(nodeB, new Point(0, 0));  // top-left corner

    var points = router.Route(source, target);
    var start = points[0];
    var cp1 = points[1];

    // Tie-break prefers horizontal → left emission → cp1.X < start.X, cp1.Y == start.Y.
    Assert.Equal(start.Y, cp1.Y);
    Assert.True(cp1.X < start.X);
}

[Fact]
public void Route_with_zero_size_owner_falls_back_to_horizontal()
{
    var router = new BezierRouter();
    var nodeA = new Node { X = 0, Y = 0 };    // Width = Height = 0
    var nodeB = new Node { X = 200, Y = 0 };  // Width = Height = 0
    var source = new Port(nodeA, new Point(0, 0));
    var target = new Port(nodeB, new Point(0, 0));

    var points = router.Route(source, target);

    Assert.All(points, p =>
    {
        Assert.False(double.IsNaN(p.X));
        Assert.False(double.IsNaN(p.Y));
        Assert.False(double.IsInfinity(p.X));
        Assert.False(double.IsInfinity(p.Y));
    });

    // With all distances tied at 0, tie-break selects horizontal left emission.
    Assert.Equal(points[0].Y, points[1].Y);
    Assert.Equal(points[3].Y, points[2].Y);
}
```

**Step 2: Run BezierRouter tests**

```bash
dotnet test --filter "FullyQualifiedName~BezierRouterTests"
```

Expected: 9 passed, 0 failed.

**Step 3: Run the full test suite**

```bash
dotnet test
```

Expected: 400 passed (395 original + 5 new), 0 failed.

**Step 4: Commit**

```bash
git add tests/NodiumGraph.Tests/BezierRouterTests.cs
git commit -m "test(BezierRouter): cover mixed, corner, and zero-size emission"
```

---

### Task 4: Update memory file

Mark the horizontal-only limitation resolved so future sessions don't rediscover the same problem.

**Files:**
- Modify: `C:\Users\metro\.claude\projects\D--Projects-Nenoso-NodiumGraph\memory\project_bezier_port_direction.md`

**Step 1: Rewrite the memory**

The description should reflect "resolved" state. Body should summarize: direction-aware classification is implemented in `BezierRouter` as a private helper; Port API unchanged; remaining future work is (a) explicit `Port.Normal` override for angled ports (YAGNI), (b) span-based `Route()` perf refactor (only if needed), (c) `StepRouter` direction awareness (deferred).

Keep the frontmatter `name` field and `originSessionId`. Update `description` to reflect resolved state.

**Step 2: Update MEMORY.md index line**

In `C:\Users\metro\.claude\projects\D--Projects-Nenoso-NodiumGraph\memory\MEMORY.md`, replace the BezierRouter line with a one-liner noting the fix landed and what's still deferred.

**Step 3: No commit** — memory files live outside the repo.

---

### Task 5: Final verification and push

**Step 1: Confirm clean state**

```bash
git status
git log --oneline -5
```

Expected: clean tree, 4 new commits above `8c6c8db`.

**Step 2: Run the full suite one more time**

```bash
dotnet test
```

Expected: 400 passed.

**Step 3: Push**

Ask the user whether to push. Do not push without confirmation.

---

## Acceptance criteria

- [ ] `BezierRouter.Route()` derives control-point direction from each port's edge on its owner node.
- [ ] No changes to `Port`, `IPortProvider`, `FixedPortProvider`, `DynamicPortProvider`, `StepRouter`, `StraightRouter`, `NodiumGraphCanvas`, or `ConnectionRenderer`.
- [ ] 9 tests in `BezierRouterTests.cs`, all passing.
- [ ] Full test suite: 400 passed.
- [ ] 4 commits on `main` following the design doc commit, each scoped to one logical change.
- [ ] Memory file updated to reflect resolved state.
