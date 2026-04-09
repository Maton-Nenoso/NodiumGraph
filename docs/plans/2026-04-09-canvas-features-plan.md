# Canvas Features Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Implement the NodiumGraphCanvas control with Tier 2 features: rendering, interactions, defaults, minimap, and special node types.

**Architecture:** Graph-centric binding (single `StyledProperty<Graph>`), hybrid rendering (DataTemplate nodes, custom-drawn connections/grid), handler-based interactions ("report, don't decide"). See `docs/plans/2026-04-09-canvas-features-design.md` for full design.

**Tech Stack:** .NET 10, Avalonia 12, xUnit v3, Avalonia.Headless.XUnit

**Existing code:** 20 source files in `src/NodiumGraph/` (Model/, Interactions/, Controls/), 55 passing tests in `tests/NodiumGraph.Tests/`. The `NodiumGraphCanvas` is currently a 10-line stub extending `TemplatedControl`.

---

## Phase 1: Model Additions

New properties and types needed before canvas work can begin.

### Task 1: Add PortFlow enum

**Files:**
- Create: `src/NodiumGraph/Model/PortFlow.cs`
- Test: `tests/NodiumGraph.Tests/PortFlowTests.cs`

**Step 1: Write the test**

```csharp
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortFlowTests
{
    [Fact]
    public void PortFlow_has_Input_and_Output_values()
    {
        Assert.Equal(0, (int)PortFlow.Input);
        Assert.Equal(1, (int)PortFlow.Output);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter PortFlowTests`
Expected: FAIL — `PortFlow` type not found

**Step 3: Implement**

```csharp
namespace NodiumGraph.Model;

public enum PortFlow
{
    Input,
    Output
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter PortFlowTests`
Expected: PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Model/PortFlow.cs tests/NodiumGraph.Tests/PortFlowTests.cs
git commit -m "Add PortFlow enum for port direction semantics"
```

---

### Task 2: Add Port.Name and Port.Flow properties

**Files:**
- Modify: `src/NodiumGraph/Model/Port.cs`
- Modify: `tests/NodiumGraph.Tests/PortTests.cs`

**Step 1: Write the tests**

Add to `PortTests.cs`:

```csharp
[Fact]
public void Port_stores_name()
{
    var node = new Node();
    var port = new Port(node, "MyPort", PortFlow.Input, new Point(0, 0));
    Assert.Equal("MyPort", port.Name);
}

[Fact]
public void Port_stores_flow()
{
    var node = new Node();
    var port = new Port(node, "Out", PortFlow.Output, new Point(0, 0));
    Assert.Equal(PortFlow.Output, port.Flow);
}

[Fact]
public void Port_name_defaults_to_empty_string()
{
    var node = new Node();
    var port = new Port(node, new Point(0, 0));
    Assert.Equal(string.Empty, port.Name);
}

[Fact]
public void Port_flow_defaults_to_Input()
{
    var node = new Node();
    var port = new Port(node, new Point(0, 0));
    Assert.Equal(PortFlow.Input, port.Flow);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter PortTests`
Expected: FAIL — constructor overload and properties not found

**Step 3: Implement**

Update `Port.cs` — add `Name` and `Flow` properties, add new constructor overload while preserving the existing `Port(Node, Point)` constructor for backward compatibility:

```csharp
using Avalonia;

namespace NodiumGraph.Model;

public class Port
{
    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }
    public Point Position { get; init; }

    public Point AbsolutePosition => new(Owner.X + Position.X, Owner.Y + Position.Y);

    public Port(Node owner, string name, PortFlow flow, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Flow = flow;
        Position = position;
    }

    public Port(Node owner, Point position) : this(owner, string.Empty, PortFlow.Input, position)
    {
    }
}
```

**Step 4: Run ALL tests to verify nothing broke**

Run: `dotnet test`
Expected: ALL PASS (existing tests use the 2-arg constructor which still works)

**Step 5: Commit**

```bash
git add src/NodiumGraph/Model/Port.cs tests/NodiumGraph.Tests/PortTests.cs
git commit -m "Add Name and Flow properties to Port"
```

---

### Task 3: Add Node.Title and Node.IsSelected properties

**Files:**
- Modify: `src/NodiumGraph/Model/Node.cs`
- Modify: `tests/NodiumGraph.Tests/NodeTests.cs`

**Step 1: Write the tests**

Add to `NodeTests.cs`:

```csharp
[Fact]
public void Title_defaults_to_type_name()
{
    var node = new Node();
    Assert.Equal("Node", node.Title);
}

[Fact]
public void Title_can_be_set_and_fires_PropertyChanged()
{
    var node = new Node();
    var fired = false;
    ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(Node.Title)) fired = true;
    };

    node.Title = "My Node";
    Assert.Equal("My Node", node.Title);
    Assert.True(fired);
}

[Fact]
public void IsSelected_defaults_to_false()
{
    var node = new Node();
    Assert.False(node.IsSelected);
}

[Fact]
public void IsSelected_fires_PropertyChanged()
{
    var node = new Node();
    var fired = false;
    ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(Node.IsSelected)) fired = true;
    };

    node.IsSelected = true;
    Assert.True(fired);
}

[Fact]
public void Subclass_title_defaults_to_subclass_name()
{
    var node = new TestNode();
    Assert.Equal("TestNode", node.Title);
}

// Add at bottom of file or as nested class
private class TestNode : Node { }
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter NodeTests`
Expected: FAIL — `Title` and `IsSelected` not found

**Step 3: Implement**

Add to `Node.cs`:

```csharp
private string _title;
private bool _isSelected;

// In constructor or field initializer:
// _title = GetType().Name;  -- initialize in the declaration

public string Title
{
    get => _title;
    set => SetField(ref _title, value);
}

public bool IsSelected
{
    get => _isSelected;
    internal set => SetField(ref _isSelected, value);
}
```

Note: `_title` must be initialized to `GetType().Name` — this must happen in the constructor (not field initializer) because `GetType()` returns the runtime type, which works correctly for subclasses.

`IsSelected` has `internal set` — only the canvas (same assembly) and tests (InternalsVisibleTo) can set it.

**Step 4: Run ALL tests**

Run: `dotnet test`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Model/Node.cs tests/NodiumGraph.Tests/NodeTests.cs
git commit -m "Add Title and IsSelected properties to Node"
```

---

## Phase 2: Built-in Connection Routers

Three `IConnectionRouter` implementations. Independent of canvas — can be tested in isolation.

### Task 4: StraightRouter

**Files:**
- Create: `src/NodiumGraph/Interactions/StraightRouter.cs`
- Create: `tests/NodiumGraph.Tests/StraightRouterTests.cs`

**Step 1: Write the tests**

```csharp
using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class StraightRouterTests
{
    [Fact]
    public void Route_returns_two_points_source_and_target()
    {
        var router = new StraightRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 50));
        var target = new Port(nodeB, new Point(0, 50));

        var points = router.Route(source, target);

        Assert.Equal(2, points.Count);
        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[1]);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter StraightRouterTests`

**Step 3: Implement**

```csharp
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

public class StraightRouter : IConnectionRouter
{
    public IReadOnlyList<Point> Route(Port source, Port target) =>
        [source.AbsolutePosition, target.AbsolutePosition];
}
```

**Step 4: Run test** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/StraightRouter.cs tests/NodiumGraph.Tests/StraightRouterTests.cs
git commit -m "Add StraightRouter implementation"
```

---

### Task 5: BezierRouter

**Files:**
- Create: `src/NodiumGraph/Interactions/BezierRouter.cs`
- Create: `tests/NodiumGraph.Tests/BezierRouterTests.cs`

**Step 1: Write the tests**

```csharp
using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class BezierRouterTests
{
    [Fact]
    public void Route_returns_four_points_for_bezier_curve()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        // Bezier: start, control1, control2, end
        Assert.Equal(4, points.Count);
        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[3]);
    }

    [Fact]
    public void Control_points_are_horizontally_offset()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        // Control points should have same Y as their anchor but offset X
        Assert.Equal(points[0].Y, points[1].Y);
        Assert.Equal(points[3].Y, points[2].Y);
        // Control point 1 should be to the right of source
        Assert.True(points[1].X > points[0].X);
        // Control point 2 should be to the left of target
        Assert.True(points[2].X < points[3].X);
    }

    [Fact]
    public void Offset_scales_with_distance()
    {
        var router = new BezierRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var source = new Port(nodeA, new Point(0, 0));

        var nodeNear = new Node { X = 100, Y = 0 };
        var targetNear = new Port(nodeNear, new Point(0, 0));

        var nodeFar = new Node { X = 500, Y = 0 };
        var targetFar = new Port(nodeFar, new Point(0, 0));

        var nearPoints = router.Route(source, targetNear);
        var farPoints = router.Route(source, targetFar);

        var nearOffset = nearPoints[1].X - nearPoints[0].X;
        var farOffset = farPoints[1].X - farPoints[0].X;

        Assert.True(farOffset > nearOffset);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter BezierRouterTests`

**Step 3: Implement**

```csharp
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

public class BezierRouter : IConnectionRouter
{
    private const double MinOffset = 30.0;

    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        var dx = Math.Abs(end.X - start.X);
        var offset = Math.Max(dx * 0.4, MinOffset);

        var cp1 = new Point(start.X + offset, start.Y);
        var cp2 = new Point(end.X - offset, end.Y);

        return [start, cp1, cp2, end];
    }
}
```

**Step 4: Run test** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/BezierRouter.cs tests/NodiumGraph.Tests/BezierRouterTests.cs
git commit -m "Add BezierRouter with automatic control point offset"
```

---

### Task 6: StepRouter

**Files:**
- Create: `src/NodiumGraph/Interactions/StepRouter.cs`
- Create: `tests/NodiumGraph.Tests/StepRouterTests.cs`

**Step 1: Write the tests**

```csharp
using Avalonia;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class StepRouterTests
{
    [Fact]
    public void Route_returns_orthogonal_segments()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        Assert.Equal(source.AbsolutePosition, points[0]);
        Assert.Equal(target.AbsolutePosition, points[^1]);

        // All segments must be horizontal or vertical
        for (int i = 0; i < points.Count - 1; i++)
        {
            var isHorizontal = Math.Abs(points[i].Y - points[i + 1].Y) < 0.001;
            var isVertical = Math.Abs(points[i].X - points[i + 1].X) < 0.001;
            Assert.True(isHorizontal || isVertical,
                $"Segment {i} is neither horizontal nor vertical: {points[i]} -> {points[i + 1]}");
        }
    }

    [Fact]
    public void Route_horizontal_aligned_returns_straight_horizontal()
    {
        var router = new StepRouter();
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));

        var points = router.Route(source, target);

        // Same Y — should be a direct horizontal line
        Assert.Equal(2, points.Count);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter StepRouterTests`

**Step 3: Implement**

```csharp
using Avalonia;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

public class StepRouter : IConnectionRouter
{
    public IReadOnlyList<Point> Route(Port source, Port target)
    {
        var start = source.AbsolutePosition;
        var end = target.AbsolutePosition;

        // If vertically aligned, straight horizontal line
        if (Math.Abs(start.Y - end.Y) < 0.001)
            return [start, end];

        // Route: horizontal from source to midpoint X, vertical, horizontal to target
        var midX = (start.X + end.X) / 2;

        return
        [
            start,
            new Point(midX, start.Y),
            new Point(midX, end.Y),
            end
        ];
    }
}
```

**Step 4: Run test** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Interactions/StepRouter.cs tests/NodiumGraph.Tests/StepRouterTests.cs
git commit -m "Add StepRouter with orthogonal Manhattan routing"
```

---

## Phase 3: Canvas Foundation

Build the canvas control incrementally. Each task adds one capability.

### Task 7: NodiumGraphCanvas StyledProperties

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `src/NodiumGraph/Controls/MinimapPosition.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasPropertyTests.cs`

**Step 1: Write the tests**

Test that all StyledProperties exist with correct defaults:

```csharp
using Avalonia;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasPropertyTests
{
    [AvaloniaFact]
    public void Graph_defaults_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.Graph);
    }

    [AvaloniaFact]
    public void ViewportZoom_defaults_to_1()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(1.0, canvas.ViewportZoom);
    }

    [AvaloniaFact]
    public void ViewportOffset_defaults_to_origin()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(default(Point), canvas.ViewportOffset);
    }

    [AvaloniaFact]
    public void MinZoom_defaults_to_0_1()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(0.1, canvas.MinZoom);
    }

    [AvaloniaFact]
    public void MaxZoom_defaults_to_5()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(5.0, canvas.MaxZoom);
    }

    [AvaloniaFact]
    public void ShowGrid_defaults_to_true()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.True(canvas.ShowGrid);
    }

    [AvaloniaFact]
    public void GridSize_defaults_to_20()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(20.0, canvas.GridSize);
    }

    [AvaloniaFact]
    public void SnapToGrid_defaults_to_false()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.SnapToGrid);
    }

    [AvaloniaFact]
    public void ShowMinimap_defaults_to_false()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.False(canvas.ShowMinimap);
    }

    [AvaloniaFact]
    public void MinimapPosition_defaults_to_BottomRight()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Equal(MinimapPosition.BottomRight, canvas.MinimapPosition);
    }

    [AvaloniaFact]
    public void Handlers_default_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.NodeHandler);
        Assert.Null(canvas.ConnectionHandler);
        Assert.Null(canvas.SelectionHandler);
        Assert.Null(canvas.CanvasHandler);
        Assert.Null(canvas.ConnectionValidator);
    }

    [AvaloniaFact]
    public void ConnectionRouter_defaults_to_BezierRouter()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.IsType<BezierRouter>(canvas.ConnectionRouter);
    }

    [AvaloniaFact]
    public void NodeTemplate_defaults_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.NodeTemplate);
    }

    [AvaloniaFact]
    public void PortTemplate_defaults_to_null()
    {
        var canvas = new NodiumGraphCanvas();
        Assert.Null(canvas.PortTemplate);
    }

    [AvaloniaFact]
    public void Graph_can_be_set()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;
        Assert.Same(graph, canvas.Graph);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter NodiumGraphCanvasPropertyTests`

**Step 3: Implement**

Create `MinimapPosition.cs`:

```csharp
namespace NodiumGraph.Controls;

public enum MinimapPosition
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft
}
```

Rewrite `NodiumGraphCanvas.cs` with all StyledProperties:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

public class NodiumGraphCanvas : TemplatedControl
{
    // -- Graph --
    public static readonly StyledProperty<Graph?> GraphProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, Graph?>(nameof(Graph));

    // -- Viewport --
    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(ViewportZoom), 1.0);

    public static readonly StyledProperty<Point> ViewportOffsetProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, Point>(nameof(ViewportOffset));

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(MinZoom), 0.1);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(MaxZoom), 5.0);

    // -- Grid --
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, double>(nameof(GridSize), 20.0);

    public static readonly StyledProperty<bool> SnapToGridProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(SnapToGrid));

    // -- Templates --
    public static readonly StyledProperty<IDataTemplate?> NodeTemplateProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IDataTemplate?>(nameof(NodeTemplate));

    public static readonly StyledProperty<IDataTemplate?> PortTemplateProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IDataTemplate?>(nameof(PortTemplate));

    // -- Connections --
    public static readonly StyledProperty<IConnectionStyle> DefaultConnectionStyleProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionStyle>(
            nameof(DefaultConnectionStyle), new ConnectionStyle());

    public static readonly StyledProperty<IConnectionRouter> ConnectionRouterProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionRouter>(
            nameof(ConnectionRouter), new BezierRouter());

    // -- Handlers --
    public static readonly StyledProperty<INodeInteractionHandler?> NodeHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, INodeInteractionHandler?>(nameof(NodeHandler));

    public static readonly StyledProperty<IConnectionHandler?> ConnectionHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionHandler?>(nameof(ConnectionHandler));

    public static readonly StyledProperty<ISelectionHandler?> SelectionHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, ISelectionHandler?>(nameof(SelectionHandler));

    public static readonly StyledProperty<ICanvasInteractionHandler?> CanvasHandlerProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, ICanvasInteractionHandler?>(nameof(CanvasHandler));

    public static readonly StyledProperty<IConnectionValidator?> ConnectionValidatorProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, IConnectionValidator?>(nameof(ConnectionValidator));

    // -- Minimap --
    public static readonly StyledProperty<bool> ShowMinimapProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, bool>(nameof(ShowMinimap));

    public static readonly StyledProperty<MinimapPosition> MinimapPositionProperty =
        AvaloniaProperty.Register<NodiumGraphCanvas, MinimapPosition>(
            nameof(MinimapPosition), MinimapPosition.BottomRight);

    // -- CLR accessors --
    public Graph? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public double ViewportZoom
    {
        get => GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    public Point ViewportOffset
    {
        get => GetValue(ViewportOffsetProperty);
        set => SetValue(ViewportOffsetProperty, value);
    }

    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public bool SnapToGrid
    {
        get => GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public IDataTemplate? NodeTemplate
    {
        get => GetValue(NodeTemplateProperty);
        set => SetValue(NodeTemplateProperty, value);
    }

    public IDataTemplate? PortTemplate
    {
        get => GetValue(PortTemplateProperty);
        set => SetValue(PortTemplateProperty, value);
    }

    public IConnectionStyle DefaultConnectionStyle
    {
        get => GetValue(DefaultConnectionStyleProperty);
        set => SetValue(DefaultConnectionStyleProperty, value);
    }

    public IConnectionRouter ConnectionRouter
    {
        get => GetValue(ConnectionRouterProperty);
        set => SetValue(ConnectionRouterProperty, value);
    }

    public INodeInteractionHandler? NodeHandler
    {
        get => GetValue(NodeHandlerProperty);
        set => SetValue(NodeHandlerProperty, value);
    }

    public IConnectionHandler? ConnectionHandler
    {
        get => GetValue(ConnectionHandlerProperty);
        set => SetValue(ConnectionHandlerProperty, value);
    }

    public ISelectionHandler? SelectionHandler
    {
        get => GetValue(SelectionHandlerProperty);
        set => SetValue(SelectionHandlerProperty, value);
    }

    public ICanvasInteractionHandler? CanvasHandler
    {
        get => GetValue(CanvasHandlerProperty);
        set => SetValue(CanvasHandlerProperty, value);
    }

    public IConnectionValidator? ConnectionValidator
    {
        get => GetValue(ConnectionValidatorProperty);
        set => SetValue(ConnectionValidatorProperty, value);
    }

    public bool ShowMinimap
    {
        get => GetValue(ShowMinimapProperty);
        set => SetValue(ShowMinimapProperty, value);
    }

    public MinimapPosition MinimapPosition
    {
        get => GetValue(MinimapPositionProperty);
        set => SetValue(MinimapPositionProperty, value);
    }
}
```

**Step 4: Run ALL tests**

Run: `dotnet test`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs src/NodiumGraph/Controls/MinimapPosition.cs tests/NodiumGraph.Tests/NodiumGraphCanvasPropertyTests.cs
git commit -m "Add all StyledProperties to NodiumGraphCanvas"
```

---

### Task 8: Canvas world-to-screen coordinate transform

**Files:**
- Create: `src/NodiumGraph/Controls/ViewportTransform.cs`
- Create: `tests/NodiumGraph.Tests/ViewportTransformTests.cs`

The canvas needs to convert between world coordinates (where nodes live) and screen coordinates (where pixels are). This is a pure math class — easy to test, foundational for everything.

**Step 1: Write the tests**

```csharp
using Avalonia;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class ViewportTransformTests
{
    [Fact]
    public void Identity_transform_at_default_zoom_and_offset()
    {
        var vt = new ViewportTransform(zoom: 1.0, offset: default);
        var world = new Point(100, 200);
        Assert.Equal(world, vt.WorldToScreen(world));
        Assert.Equal(world, vt.ScreenToWorld(world));
    }

    [Fact]
    public void Offset_shifts_world_to_screen()
    {
        var vt = new ViewportTransform(zoom: 1.0, offset: new Point(50, 100));
        // World (0,0) should appear at screen (50,100)
        Assert.Equal(new Point(50, 100), vt.WorldToScreen(new Point(0, 0)));
    }

    [Fact]
    public void Screen_to_world_reverses_offset()
    {
        var vt = new ViewportTransform(zoom: 1.0, offset: new Point(50, 100));
        Assert.Equal(new Point(0, 0), vt.ScreenToWorld(new Point(50, 100)));
    }

    [Fact]
    public void Zoom_scales_world_to_screen()
    {
        var vt = new ViewportTransform(zoom: 2.0, offset: default);
        Assert.Equal(new Point(200, 400), vt.WorldToScreen(new Point(100, 200)));
    }

    [Fact]
    public void Screen_to_world_reverses_zoom()
    {
        var vt = new ViewportTransform(zoom: 2.0, offset: default);
        Assert.Equal(new Point(100, 200), vt.ScreenToWorld(new Point(200, 400)));
    }

    [Fact]
    public void Zoom_and_offset_combined()
    {
        var vt = new ViewportTransform(zoom: 2.0, offset: new Point(10, 20));
        // World (50,100) -> screen: (50*2 + 10, 100*2 + 20) = (110, 220)
        Assert.Equal(new Point(110, 220), vt.WorldToScreen(new Point(50, 100)));
        Assert.Equal(new Point(50, 100), vt.ScreenToWorld(new Point(110, 220)));
    }

    [Fact]
    public void Roundtrip_preserves_point()
    {
        var vt = new ViewportTransform(zoom: 1.5, offset: new Point(37, -42));
        var original = new Point(123.456, -789.012);
        var roundtrip = vt.ScreenToWorld(vt.WorldToScreen(original));
        Assert.Equal(original.X, roundtrip.X, 6);
        Assert.Equal(original.Y, roundtrip.Y, 6);
    }
}
```

**Step 2: Run tests** → FAIL

**Step 3: Implement**

```csharp
using Avalonia;

namespace NodiumGraph.Controls;

/// <summary>
/// Converts between world coordinates (where nodes live) and screen coordinates (pixels).
/// Transform: screen = world * zoom + offset
/// </summary>
public readonly struct ViewportTransform(double zoom, Point offset)
{
    public double Zoom { get; } = zoom;
    public Point Offset { get; } = offset;

    public Point WorldToScreen(Point world) =>
        new(world.X * Zoom + Offset.X, world.Y * Zoom + Offset.Y);

    public Point ScreenToWorld(Point screen) =>
        new((screen.X - Offset.X) / Zoom, (screen.Y - Offset.Y) / Zoom);

    public double WorldToScreen(double worldLength) => worldLength * Zoom;
    public double ScreenToWorld(double screenLength) => screenLength / Zoom;
}
```

**Step 4: Run tests** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/ViewportTransform.cs tests/NodiumGraph.Tests/ViewportTransformTests.cs
git commit -m "Add ViewportTransform for world/screen coordinate conversion"
```

---

### Task 9: Canvas graph subscription (add/remove nodes and connections)

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs`

When `Graph` property changes, the canvas must subscribe to `Graph.Nodes.CollectionChanged` and `Graph.Connections.CollectionChanged` to add/remove visual children. This task focuses on the subscription plumbing and child management — not rendering.

**Step 1: Write the tests**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NodiumGraph.Controls;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class NodiumGraphCanvasGraphBindingTests
{
    [AvaloniaFact]
    public void Setting_graph_with_existing_nodes_creates_child_controls()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node { X = 10, Y = 20 });
        graph.AddNode(new Node { X = 30, Y = 40 });

        canvas.Graph = graph;

        Assert.Equal(2, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Adding_node_to_graph_creates_child_control()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        canvas.Graph = graph;

        graph.AddNode(new Node());

        Assert.Equal(1, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Removing_node_from_graph_removes_child_control()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        canvas.Graph = graph;

        graph.RemoveNode(node);

        Assert.Equal(0, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Changing_graph_clears_old_and_loads_new()
    {
        var canvas = new NodiumGraphCanvas();
        var graph1 = new Graph();
        graph1.AddNode(new Node());
        graph1.AddNode(new Node());
        canvas.Graph = graph1;

        var graph2 = new Graph();
        graph2.AddNode(new Node());
        canvas.Graph = graph2;

        Assert.Equal(1, canvas.NodeContainerCount);
    }

    [AvaloniaFact]
    public void Setting_graph_to_null_clears_children()
    {
        var canvas = new NodiumGraphCanvas();
        var graph = new Graph();
        graph.AddNode(new Node());
        canvas.Graph = graph;

        canvas.Graph = null;

        Assert.Equal(0, canvas.NodeContainerCount);
    }
}
```

**Step 2: Run tests** → FAIL (NodeContainerCount does not exist)

**Step 3: Implement**

Add to `NodiumGraphCanvas.cs` — graph subscription logic, internal node container tracking, `NodeContainerCount` (internal for testing):

- Override `OnPropertyChanged` to detect `GraphProperty` changes
- Subscribe to `Nodes.CollectionChanged`
- Maintain a `Dictionary<Node, ContentControl>` mapping nodes to their visual containers
- Expose `internal int NodeContainerCount` for testing

This is the first piece of real canvas logic. Keep it focused on container management — no rendering, no positioning, no input handling yet.

**Step 4: Run ALL tests** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs tests/NodiumGraph.Tests/NodiumGraphCanvasGraphBindingTests.cs
git commit -m "Add graph subscription and node container management to canvas"
```

---

## Phase 4: Rendering

### Task 10: Grid rendering

**Files:**
- Create: `src/NodiumGraph/Controls/GridRenderer.cs`
- Create: `tests/NodiumGraph.Tests/GridRendererTests.cs`

Extract grid rendering into a standalone class that draws to a `DrawingContext`. Test that it respects grid size, visibility, and viewport transform.

**Step 1: Write the tests**

Test the grid point calculation logic (not the actual drawing — that requires visual verification):

```csharp
using Avalonia;
using NodiumGraph.Controls;
using Xunit;

namespace NodiumGraph.Tests;

public class GridRendererTests
{
    [Fact]
    public void ComputeGridLines_returns_points_within_visible_area()
    {
        var transform = new ViewportTransform(1.0, default);
        var visibleArea = new Rect(0, 0, 100, 100);

        var points = GridRenderer.ComputeGridPoints(visibleArea, transform, gridSize: 20.0);

        // Grid at 20px intervals in a 100x100 area = 6x6 = 36 points (0,20,40,60,80,100)
        Assert.All(points, p =>
        {
            Assert.InRange(p.X, 0, 100);
            Assert.InRange(p.Y, 0, 100);
        });
        Assert.True(points.Count > 0);
    }

    [Fact]
    public void ComputeGridLines_respects_zoom()
    {
        var transform = new ViewportTransform(2.0, default);
        var visibleArea = new Rect(0, 0, 200, 200);

        var pointsZoom2 = GridRenderer.ComputeGridPoints(visibleArea, transform, gridSize: 20.0);

        var transformNoZoom = new ViewportTransform(1.0, default);
        var pointsZoom1 = GridRenderer.ComputeGridPoints(visibleArea, transformNoZoom, gridSize: 20.0);

        // At 2x zoom, fewer world-space grid lines fit in the same screen area
        Assert.True(pointsZoom2.Count <= pointsZoom1.Count);
    }
}
```

**Step 2: Run tests** → FAIL

**Step 3: Implement**

```csharp
using Avalonia;
using Avalonia.Media;

namespace NodiumGraph.Controls;

internal static class GridRenderer
{
    public static IReadOnlyList<Point> ComputeGridPoints(
        Rect visibleScreenArea, ViewportTransform transform, double gridSize)
    {
        var topLeft = transform.ScreenToWorld(visibleScreenArea.TopLeft);
        var bottomRight = transform.ScreenToWorld(visibleScreenArea.BottomRight);

        var startX = Math.Floor(topLeft.X / gridSize) * gridSize;
        var startY = Math.Floor(topLeft.Y / gridSize) * gridSize;

        var points = new List<Point>();
        for (var x = startX; x <= bottomRight.X; x += gridSize)
        {
            for (var y = startY; y <= bottomRight.Y; y += gridSize)
            {
                points.Add(transform.WorldToScreen(new Point(x, y)));
            }
        }

        return points;
    }

    public static void Render(DrawingContext context, Rect bounds,
        ViewportTransform transform, double gridSize, IBrush dotBrush)
    {
        var points = ComputeGridPoints(bounds, transform, gridSize);
        var dotRadius = Math.Max(1.0, 1.5 * transform.Zoom);

        foreach (var pt in points)
            context.DrawEllipse(dotBrush, null, pt, dotRadius, dotRadius);
    }
}
```

**Step 4: Run tests** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/GridRenderer.cs tests/NodiumGraph.Tests/GridRendererTests.cs
git commit -m "Add GridRenderer for dot grid with zoom-aware spacing"
```

---

### Task 11: Connection rendering

**Files:**
- Create: `src/NodiumGraph/Controls/ConnectionRenderer.cs`
- Create: `tests/NodiumGraph.Tests/ConnectionRendererTests.cs`

Renders connections using `IConnectionRouter` for path and `IConnectionStyle` for appearance.

**Step 1: Write the tests**

Test the geometry creation (not visual output):

```csharp
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Controls;
using NodiumGraph.Interactions;
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionRendererTests
{
    [Fact]
    public void CreateGeometry_with_straight_router_returns_line()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 200, Y = 100 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new StraightRouter();
        var transform = new ViewportTransform(1.0, default);

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, transform);

        Assert.NotNull(geometry);
    }

    [Fact]
    public void CreateGeometry_with_bezier_router_returns_bezier_curve()
    {
        var nodeA = new Node { X = 0, Y = 0 };
        var nodeB = new Node { X = 300, Y = 0 };
        var source = new Port(nodeA, new Point(100, 25));
        var target = new Port(nodeB, new Point(0, 25));
        var connection = new Connection(source, target);
        var router = new BezierRouter();
        var transform = new ViewportTransform(1.0, default);

        var geometry = ConnectionRenderer.CreateGeometry(connection, router, transform);

        Assert.NotNull(geometry);
    }
}
```

**Step 2: Run tests** → FAIL

**Step 3: Implement**

```csharp
using Avalonia;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Controls;

internal static class ConnectionRenderer
{
    public static Geometry CreateGeometry(
        Connection connection, IConnectionRouter router, ViewportTransform transform)
    {
        var routePoints = router.Route(connection.SourcePort, connection.TargetPort);
        var screenPoints = routePoints.Select(transform.WorldToScreen).ToList();

        if (screenPoints.Count < 2)
            return new LineGeometry();

        // 4 points = bezier, otherwise polyline
        if (screenPoints.Count == 4)
        {
            var fig = new PathFigure { StartPoint = screenPoints[0], IsClosed = false };
            fig.Segments!.Add(new BezierSegment
            {
                Point1 = screenPoints[1],
                Point2 = screenPoints[2],
                Point3 = screenPoints[3]
            });
            var geo = new PathGeometry();
            geo.Figures!.Add(fig);
            return geo;
        }

        // Polyline for straight/step routes
        var figure = new PathFigure { StartPoint = screenPoints[0], IsClosed = false };
        for (int i = 1; i < screenPoints.Count; i++)
            figure.Segments!.Add(new LineSegment { Point = screenPoints[i] });

        var pathGeo = new PathGeometry();
        pathGeo.Figures!.Add(figure);
        return pathGeo;
    }

    public static void Render(DrawingContext context, Connection connection,
        IConnectionRouter router, IConnectionStyle style, ViewportTransform transform)
    {
        var geometry = CreateGeometry(connection, router, transform);
        var pen = new Pen(style.Stroke, style.Thickness, style.DashPattern);
        context.DrawGeometry(null, pen, geometry);
    }
}
```

**Step 4: Run tests** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Controls/ConnectionRenderer.cs tests/NodiumGraph.Tests/ConnectionRendererTests.cs
git commit -m "Add ConnectionRenderer with bezier and polyline geometry creation"
```

---

### Task 12: Canvas OnRender — compositing grid + connections

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`

Override `Render` on the canvas to draw grid and connections in correct order. Node controls are positioned in the visual tree (handled by Task 9's container logic).

**Step 1: Verify build**

Run: `dotnet build`
Expected: PASS — this is integration work connecting existing pieces

**Step 2: Implement**

Add `Render` override to `NodiumGraphCanvas.cs` that:
1. Computes `ViewportTransform` from current `ViewportZoom` and `ViewportOffset`
2. If `ShowGrid`, calls `GridRenderer.Render()`
3. For each connection in `Graph.Connections`, calls `ConnectionRenderer.Render()`
4. Calls `InvalidateVisual()` when relevant properties change

**Step 3: Run ALL tests**

Run: `dotnet test`
Expected: ALL PASS

**Step 4: Commit**

```bash
git add src/NodiumGraph/Controls/NodiumGraphCanvas.cs
git commit -m "Add render pipeline compositing grid and connections"
```

---

## Phase 5: Interactions

Each interaction is a separate task. All follow the same pattern: handle pointer/key events on the canvas, translate to world coordinates, perform the interaction logic, report to handler.

### Task 13: Pan — middle-mouse drag and Space+left-drag

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasPanTests.cs`

**Tests:** Verify that `ViewportOffset` changes in response to simulated pointer input.

**Implementation:** Handle `PointerPressed`/`PointerMoved`/`PointerReleased`. Track pan state (active, start screen point, original offset). On move, compute delta and update `ViewportOffset`.

**Commit message:** `"Add pan interaction (middle-mouse and Space+left-drag)"`

---

### Task 14: Zoom — scroll wheel toward cursor

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasZoomTests.cs`

**Tests:** Verify that `ViewportZoom` changes on scroll, clamped to min/max, and that the world point under cursor stays fixed.

**Implementation:** Handle `PointerWheelChanged`. Compute new zoom, clamp to `MinZoom`/`MaxZoom`, adjust `ViewportOffset` so the world point under the cursor stays fixed:

```csharp
var cursorWorld = transform.ScreenToWorld(cursorScreen);
ViewportZoom = newZoom;
ViewportOffset = new Point(
    cursorScreen.X - cursorWorld.X * newZoom,
    cursorScreen.Y - cursorWorld.Y * newZoom);
```

**Commit message:** `"Add scroll-wheel zoom toward cursor with min/max clamping"`

---

### Task 15: Node selection — click, Ctrl+click, marquee

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasSelectionTests.cs`

**Tests:**
- Click on node → clears selection, selects clicked node
- Ctrl+click → toggles node in selection
- Click on empty → clears selection
- Marquee drag → selects all nodes within rectangle
- Ctrl+marquee → adds to existing selection
- Escape → clears selection

**Implementation:**
- Hit-test pointer position against node container bounds
- Use `Graph.Select()`, `Graph.Deselect()`, `Graph.ClearSelection()`
- Set `Node.IsSelected` accordingly
- Report to `SelectionHandler?.OnSelectionChanged()`
- Draw marquee rectangle during drag (add to render pipeline)

**Commit message:** `"Add node selection (click, Ctrl+click, marquee, Ctrl+marquee)"`

---

### Task 16: Node drag — single and multi with snap-to-grid

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasDragTests.cs`

**Tests:**
- Left-drag on selected node → moves it (verify X/Y changed)
- Drag with multi-selection → all selected nodes move by same delta
- With `SnapToGrid=true` → node positions round to `GridSize`
- On drag complete → `NodeHandler?.OnNodesMoved()` called with correct `NodeMoveInfo`

**Implementation:**
- Track drag state (active, start world positions of all selected nodes, pointer start)
- On move: compute world delta, apply to all selected nodes
- If `SnapToGrid`: round positions to `GridSize` multiples
- On release: collect `NodeMoveInfo` list, report to handler

**Commit message:** `"Add node drag with multi-selection and snap-to-grid"`

---

### Task 17: Connection draw — port drag with validation feedback

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasConnectionDrawTests.cs`

**Tests:**
- Drag from port → connection preview line follows cursor
- Hover over valid target port → `ConnectionValidator?.CanConnect()` returns true → green feedback
- Hover over invalid target → red feedback
- Release on valid target → `ConnectionHandler?.OnConnectionRequested()` called
- Release on empty space → preview cancelled, no handler call

**Implementation:**
- Hit-test pointer against port visuals on `PointerPressed`
- Track connection draw state (source port, current cursor world position)
- During drag: render preview connection (dashed line from source port to cursor)
- On each target port hover: call `ConnectionValidator?.CanConnect()`
- On release on port: call `ConnectionHandler?.OnConnectionRequested()`
- Add preview rendering to render pipeline (between connections and node containers layer)

**Commit message:** `"Add connection draw interaction with validation feedback"`

---

### Task 18: Delete — keyboard handler

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasKeyboardTests.cs`

**Tests:**
- Delete key with nodes selected → `NodeHandler?.OnDeleteRequested()` called with selected nodes and their connections
- Ctrl+A → all nodes selected
- Escape → selection cleared

**Implementation:**
- Handle `KeyDown` event
- `Key.Delete` → collect selected nodes and all connections touching them, report to `NodeHandler?.OnDeleteRequested()`
- `Key.A` with Ctrl → `Graph.Select()` all nodes, report to `SelectionHandler`
- `Key.Escape` → `Graph.ClearSelection()`, cancel any active drag/connection draw

**Commit message:** `"Add keyboard shortcuts (Delete, Ctrl+A, Escape)"`

---

## Phase 6: Public Methods

### Task 19: ZoomToFit, ZoomToNodes, CenterOnNode, SelectAll, DeleteSelected

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasMethodTests.cs`

**Tests:**

```csharp
[AvaloniaFact]
public void ZoomToFit_sets_viewport_to_contain_all_nodes()
{
    var canvas = new NodiumGraphCanvas();
    var graph = new Graph();
    var n1 = new Node { X = 0, Y = 0 };
    n1.Width = 100; n1.Height = 50;
    var n2 = new Node { X = 500, Y = 300 };
    n2.Width = 100; n2.Height = 50;
    graph.AddNode(n1);
    graph.AddNode(n2);
    canvas.Graph = graph;

    // Simulate canvas having a known size
    // canvas.ZoomToFit() should adjust ViewportZoom and ViewportOffset
    // so that all nodes are visible
}
```

**Implementation:**
- `ZoomToFit(padding)` — compute bounding rect of all nodes, set zoom and offset to fit within canvas bounds
- `ZoomToNodes(nodes, padding)` — same but for a subset
- `CenterOnNode(node)` — set offset so node is centered, don't change zoom
- `SelectAll()` — select all nodes via `Graph.Select()`
- `DeleteSelected()` — report to `NodeHandler?.OnDeleteRequested()`

**Commit message:** `"Add public methods: ZoomToFit, ZoomToNodes, CenterOnNode, SelectAll, DeleteSelected"`

---

## Phase 7: Advanced Features (Tier 2)

### Task 20: Auto-pan on edge drag

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`

When dragging a node or drawing a connection near the canvas edges, automatically scroll the viewport. Configurable margin and speed.

**Implementation:**
- During active drag, check if pointer is within edge margin (e.g., 40px)
- If so, adjust `ViewportOffset` by a small delta each frame using a timer
- Stop auto-pan when pointer moves away from edge or drag ends

**Commit message:** `"Add auto-pan when dragging near canvas edges"`

---

### Task 21: Connection cutting

**Files:**
- Modify: `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`
- Create: `tests/NodiumGraph.Tests/NodiumGraphCanvasConnectionCuttingTests.cs`

**Tests:**
- Alt+left-drag draws a cutting line
- Connections that intersect the cutting line are reported to `ConnectionHandler?.OnConnectionDeleteRequested()`
- Cutting line visual appears during drag, disappears on release

**Implementation:**
- Detect Alt+left-drag → enter cutting mode
- Track cutting line (start, end)
- On release: find all connections whose geometry intersects the cutting line
- For each intersected connection: call `ConnectionHandler?.OnConnectionDeleteRequested()`
- Render cutting line (dashed red) during drag

**Commit message:** `"Add connection cutting via Alt+left-drag"`

---

### Task 22: Minimap control

**Files:**
- Create: `src/NodiumGraph/Controls/Minimap.cs`
- Create: `tests/NodiumGraph.Tests/MinimapTests.cs`

**Tests:**
- Minimap renders node rectangles proportional to world positions
- Viewport rectangle shown on minimap matches current zoom/offset
- Click on minimap → viewport jumps to that world position

**Implementation:**
- Internal control rendered as overlay in canvas corner (per `MinimapPosition`)
- Subscribes to same `Graph` as parent canvas
- Renders simplified node rectangles (filled boxes, no templates)
- Renders viewport rectangle (translucent overlay)
- On click: compute corresponding world position, set parent canvas `ViewportOffset`
- On drag: continuous viewport positioning

**Commit message:** `"Add minimap overlay with click-to-navigate"`

---

### Task 23: GroupNode and CommentNode base classes

**Files:**
- Create: `src/NodiumGraph/Model/GroupNode.cs`
- Create: `src/NodiumGraph/Model/CommentNode.cs`
- Create: `tests/NodiumGraph.Tests/GroupNodeTests.cs`
- Create: `tests/NodiumGraph.Tests/CommentNodeTests.cs`

**Step 1: Write the tests**

```csharp
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class GroupNodeTests
{
    [Fact]
    public void GroupNode_extends_Node()
    {
        var group = new GroupNode();
        Assert.IsAssignableFrom<Node>(group);
    }

    [Fact]
    public void Children_starts_empty()
    {
        var group = new GroupNode();
        Assert.Empty(group.Children);
    }

    [Fact]
    public void Can_add_and_remove_children()
    {
        var group = new GroupNode();
        var child = new Node();
        group.AddChild(child);
        Assert.Contains(child, group.Children);

        group.RemoveChild(child);
        Assert.DoesNotContain(child, group.Children);
    }

    [Fact]
    public void Title_defaults_to_Group()
    {
        var group = new GroupNode();
        Assert.Equal("GroupNode", group.Title);
    }
}
```

```csharp
using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class CommentNodeTests
{
    [Fact]
    public void CommentNode_extends_Node()
    {
        var comment = new CommentNode();
        Assert.IsAssignableFrom<Node>(comment);
    }

    [Fact]
    public void Comment_defaults_to_empty()
    {
        var comment = new CommentNode();
        Assert.Equal(string.Empty, comment.Comment);
    }

    [Fact]
    public void Comment_fires_PropertyChanged()
    {
        var comment = new CommentNode();
        var fired = false;
        comment.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommentNode.Comment)) fired = true;
        };
        comment.Comment = "Hello";
        Assert.True(fired);
    }
}
```

**Step 2: Run tests** → FAIL

**Step 3: Implement**

`GroupNode.cs`:

```csharp
using System.Collections.ObjectModel;

namespace NodiumGraph.Model;

public class GroupNode : Node
{
    private readonly ObservableCollection<Node> _children = new();

    public ReadOnlyObservableCollection<Node> Children { get; }

    public GroupNode()
    {
        Children = new(_children);
    }

    public void AddChild(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_children.Contains(node))
            _children.Add(node);
    }

    public void RemoveChild(Node node)
    {
        _children.Remove(node);
    }
}
```

`CommentNode.cs`:

```csharp
namespace NodiumGraph.Model;

public class CommentNode : Node
{
    private string _comment = string.Empty;

    public string Comment
    {
        get => _comment;
        set => SetField(ref _comment, value);
    }
}
```

**Step 4: Run tests** → PASS

**Step 5: Commit**

```bash
git add src/NodiumGraph/Model/GroupNode.cs src/NodiumGraph/Model/CommentNode.cs tests/NodiumGraph.Tests/GroupNodeTests.cs tests/NodiumGraph.Tests/CommentNodeTests.cs
git commit -m "Add GroupNode and CommentNode base classes"
```

---

## Phase 8: Default Templates & Theme

### Task 24: Default node ControlTemplate (AXAML)

**Files:**
- Create: `src/NodiumGraph/Themes/Generic.axaml` (default styles/templates)
- Modify: `src/NodiumGraph/NodiumGraph.csproj` (include AXAML resources)

This is where the default Header+Body node visual lives. It's an Avalonia ControlTemplate/DataTemplate shipped as a resource in the library assembly.

**Implementation:**
- Create `Themes/Generic.axaml` with:
  - Default style for `NodiumGraphCanvas`
  - Default DataTemplate for `Node` (header with title, body ContentPresenter, port visuals)
  - Default DataTemplate for `GroupNode` (resizable box with title)
  - Default DataTemplate for `CommentNode` (colored text label)
- Update `.csproj` to include AXAML as `AvaloniaResource`
- Use theme resource references (`DynamicResource`) for all colors

**Commit message:** `"Add default themes with node, group, and comment templates"`

---

### Task 25: Sample app integration

**Files:**
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml`
- Modify: `samples/NodiumGraph.Sample/MainWindow.axaml.cs`

Wire up a minimal working sample that demonstrates:
- A `Graph` with 3-4 nodes of different types
- Connections between them
- Default visuals
- Basic handler that allows creating/deleting connections

This is the "5-minute quickstart" proof — if it works, the defaults are good.

**Commit message:** `"Wire sample app with graph, nodes, connections, and default visuals"`

---

## Task Summary

| # | Task | Phase | Depends On |
|---|------|-------|------------|
| 1 | PortFlow enum | Model | — |
| 2 | Port.Name, Port.Flow | Model | 1 |
| 3 | Node.Title, Node.IsSelected | Model | — |
| 4 | StraightRouter | Routers | — |
| 5 | BezierRouter | Routers | — |
| 6 | StepRouter | Routers | — |
| 7 | Canvas StyledProperties | Canvas | 3, 5 |
| 8 | ViewportTransform | Canvas | — |
| 9 | Canvas graph subscription | Canvas | 7 |
| 10 | Grid rendering | Rendering | 8 |
| 11 | Connection rendering | Rendering | 4, 5, 6, 8 |
| 12 | Canvas OnRender compositing | Rendering | 9, 10, 11 |
| 13 | Pan interaction | Interactions | 8, 12 |
| 14 | Zoom interaction | Interactions | 8, 12 |
| 15 | Node selection | Interactions | 9, 12 |
| 16 | Node drag | Interactions | 15 |
| 17 | Connection draw | Interactions | 11, 15 |
| 18 | Keyboard shortcuts | Interactions | 15 |
| 19 | Public methods | Methods | 14, 15 |
| 20 | Auto-pan | Advanced | 13, 16, 17 |
| 21 | Connection cutting | Advanced | 11, 17 |
| 22 | Minimap | Advanced | 9, 14 |
| 23 | GroupNode, CommentNode | Model | 3 |
| 24 | Default templates & theme | Rendering | 7, 9, 23 |
| 25 | Sample app integration | Integration | 24 |
