# Core Types Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Merge into a single `NodiumGraph` project and implement all 18 core types (model classes, port providers, result pattern, handler interfaces, strategy interfaces).

**Architecture:** Single project `src/NodiumGraph/` with namespace `NodiumGraph`. Concrete unsealed model classes with INPC. Port provider strategy pattern. Generic Result pattern. Handler and strategy interfaces for consumer extension points.

**Tech Stack:** .NET 10, Avalonia 12.0.0, xUnit v3, Avalonia.Headless.XUnit

---

### Task 0: Merge projects into single NodiumGraph

**Files:**
- Create: `src/NodiumGraph/NodiumGraph.csproj`
- Move: `src/NodiumGraph.Core/INode.cs` → `src/NodiumGraph/` (then delete)
- Move: `src/NodiumGraph.Avalonia/NodiumGraphCanvas.cs` → `src/NodiumGraph/` (then delete)
- Delete: `src/NodiumGraph.Core/` (entire directory)
- Delete: `src/NodiumGraph.Avalonia/` (entire directory)
- Create: `tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`
- Move: headless setup from `tests/NodiumGraph.Avalonia.Tests/` → `tests/NodiumGraph.Tests/`
- Move: placeholder test from `tests/NodiumGraph.Core.Tests/` → `tests/NodiumGraph.Tests/`
- Delete: `tests/NodiumGraph.Core.Tests/` (entire directory)
- Delete: `tests/NodiumGraph.Avalonia.Tests/` (entire directory)
- Modify: `samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj` — update ProjectReference
- Modify: `NodiumGraph.sln` — update project entries

**Step 1: Create new merged project**

Create `src/NodiumGraph/NodiumGraph.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Avalonia" />
  </ItemGroup>

</Project>
```

**Step 2: Move source files and update namespaces**

Move `INode.cs` from `src/NodiumGraph.Core/` to `src/NodiumGraph/`. Update namespace:

```csharp
namespace NodiumGraph;
```

Move `NodiumGraphCanvas.cs` from `src/NodiumGraph.Avalonia/` to `src/NodiumGraph/`. Update namespace:

```csharp
using Avalonia.Controls.Primitives;

namespace NodiumGraph;

public class NodiumGraphCanvas : TemplatedControl
{
}
```

**Step 3: Create merged test project**

Create `tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Headless.XUnit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\NodiumGraph\NodiumGraph.csproj" />
  </ItemGroup>

</Project>
```

Move headless setup files from `tests/NodiumGraph.Avalonia.Tests/`:
- `App.axaml` — update `x:Class` to `NodiumGraph.Tests.App`
- `App.axaml.cs` — update namespace to `NodiumGraph.Tests`
- `TestAppBuilder.cs` — update namespace to `NodiumGraph.Tests`

Create `tests/NodiumGraph.Tests/PlaceholderTests.cs`:

```csharp
using NodiumGraph;
using Xunit;

namespace NodiumGraph.Tests;

public class PlaceholderTests
{
    [Fact]
    public void INode_interface_is_accessible()
    {
        var type = typeof(INode);
        Assert.NotNull(type);
    }
}
```

Create `tests/NodiumGraph.Tests/CanvasPlaceholderTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace NodiumGraph.Tests;

public class CanvasPlaceholderTests
{
    [AvaloniaFact]
    public void NodiumGraphCanvas_can_be_created()
    {
        var canvas = new NodiumGraphCanvas();
        var window = new Window { Content = canvas };
        window.Show();
        Assert.NotNull(canvas);
    }
}
```

**Step 4: Update sample project reference**

Modify `samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj`:

```xml
<ProjectReference Include="..\..\src\NodiumGraph\NodiumGraph.csproj" />
```

**Step 5: Update solution file**

```bash
cd D:/Projects/Nenoso/NodiumGraph/.worktrees/core-interfaces
dotnet sln remove src/NodiumGraph.Core/NodiumGraph.Core.csproj
dotnet sln remove src/NodiumGraph.Avalonia/NodiumGraph.Avalonia.csproj
dotnet sln remove tests/NodiumGraph.Core.Tests/NodiumGraph.Core.Tests.csproj
dotnet sln remove tests/NodiumGraph.Avalonia.Tests/NodiumGraph.Avalonia.Tests.csproj
dotnet sln add src/NodiumGraph/NodiumGraph.csproj
dotnet sln add tests/NodiumGraph.Tests/NodiumGraph.Tests.csproj
```

**Step 6: Delete old directories**

```bash
rm -rf src/NodiumGraph.Core src/NodiumGraph.Avalonia
rm -rf tests/NodiumGraph.Core.Tests tests/NodiumGraph.Avalonia.Tests
```

**Step 7: Build and test**

```bash
dotnet build NodiumGraph.sln
dotnet test NodiumGraph.sln
```

Expected: Build succeeds, 2 tests pass.

**Step 8: Commit**

```bash
git add -A
git commit -m "Merge into single NodiumGraph project

Combine NodiumGraph.Core and NodiumGraph.Avalonia into src/NodiumGraph/.
Combine test projects into tests/NodiumGraph.Tests/.
Single namespace: NodiumGraph."
```

---

### Task 1: Result pattern (Error, Result, Result\<T\>)

**Files:**
- Create: `src/NodiumGraph/Error.cs`
- Create: `src/NodiumGraph/Result.cs`
- Create: `src/NodiumGraph/ResultT.cs`
- Create: `tests/NodiumGraph.Tests/ResultTests.cs`

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/ResultTests.cs`:

```csharp
using Xunit;

namespace NodiumGraph.Tests;

public class ResultTests
{
    [Fact]
    public void Success_returns_successful_result()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_returns_failed_result_with_error()
    {
        var error = new Error("Something went wrong");
        var result = Result.Failure(error);
        Assert.False(result.IsSuccess);
        Assert.Equal("Something went wrong", result.Error!.Message);
    }

    [Fact]
    public void Failure_with_code_preserves_code()
    {
        var error = new Error("Bad input", "INVALID_INPUT");
        Assert.Equal("INVALID_INPUT", error.Code);
    }

    [Fact]
    public void Implicit_conversion_from_error_to_result()
    {
        var error = new Error("fail");
        Result result = error;
        Assert.False(result.IsSuccess);
        Assert.Equal("fail", result.Error!.Message);
    }

    [Fact]
    public void Failure_with_null_error_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }
}

public class ResultOfTTests
{
    [Fact]
    public void Implicit_conversion_from_value_creates_success()
    {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_error_creates_failure()
    {
        Result<int> result = new Error("nope");
        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Success_result_has_no_error()
    {
        Result<string> result = "hello";
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failed_result_has_default_value()
    {
        Result<string> result = new Error("fail");
        Assert.Null(result.Value);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~ResultTests"
```

Expected: FAIL — types not defined.

**Step 3: Implement Error**

Create `src/NodiumGraph/Error.cs`:

```csharp
namespace NodiumGraph;

public record Error(string Message, string? Code = null);
```

**Step 4: Implement Result**

Create `src/NodiumGraph/Result.cs`:

```csharp
namespace NodiumGraph;

public record Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) =>
        new(false, error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error) => Failure(error);
}
```

**Step 5: Implement Result\<T\>**

Create `src/NodiumGraph/ResultT.cs`:

```csharp
namespace NodiumGraph;

public record Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true, null) => Value = value;
    private Result(Error error) : base(false, error) { }

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
```

**Step 6: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~ResultTests"
```

Expected: 9 tests pass.

**Step 7: Commit**

```bash
git add src/NodiumGraph/Error.cs src/NodiumGraph/Result.cs src/NodiumGraph/ResultT.cs tests/NodiumGraph.Tests/ResultTests.cs
git commit -m "Add Result pattern (Error, Result, Result<T>)"
```

---

### Task 2: Node class with INotifyPropertyChanged

**Files:**
- Create: `src/NodiumGraph/Node.cs`
- Delete: `src/NodiumGraph/INode.cs`
- Create: `tests/NodiumGraph.Tests/NodeTests.cs`
- Modify: `tests/NodiumGraph.Tests/PlaceholderTests.cs` — update to reference `Node`

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/NodeTests.cs`:

```csharp
using System.ComponentModel;
using Xunit;

namespace NodiumGraph.Tests;

public class NodeTests
{
    [Fact]
    public void New_node_has_unique_id()
    {
        var node1 = new Node();
        var node2 = new Node();
        Assert.NotEqual(node1.Id, node2.Id);
    }

    [Fact]
    public void Default_position_is_zero()
    {
        var node = new Node();
        Assert.Equal(0.0, node.X);
        Assert.Equal(0.0, node.Y);
    }

    [Fact]
    public void Setting_X_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        string? propertyName = null;

        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            fired = true;
            propertyName = e.PropertyName;
        };

        node.X = 100.0;

        Assert.True(fired);
        Assert.Equal(nameof(Node.X), propertyName);
    }

    [Fact]
    public void Setting_Y_fires_PropertyChanged()
    {
        var node = new Node();
        var fired = false;
        string? propertyName = null;

        ((INotifyPropertyChanged)node).PropertyChanged += (_, e) =>
        {
            fired = true;
            propertyName = e.PropertyName;
        };

        node.Y = 200.0;

        Assert.True(fired);
        Assert.Equal(nameof(Node.Y), propertyName);
    }

    [Fact]
    public void Setting_same_X_value_does_not_fire_PropertyChanged()
    {
        var node = new Node { X = 50.0 };
        var fired = false;

        ((INotifyPropertyChanged)node).PropertyChanged += (_, _) => fired = true;

        node.X = 50.0;

        Assert.False(fired);
    }

    [Fact]
    public void Width_and_Height_default_to_zero()
    {
        var node = new Node();
        Assert.Equal(0.0, node.Width);
        Assert.Equal(0.0, node.Height);
    }

    [Fact]
    public void PortProvider_defaults_to_null()
    {
        var node = new Node();
        Assert.Null(node.PortProvider);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~NodeTests"
```

Expected: FAIL — `Node` class not defined.

**Step 3: Implement Node**

Delete `src/NodiumGraph/INode.cs`.

Create `src/NodiumGraph/Node.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NodiumGraph;

public class Node : INotifyPropertyChanged
{
    private double _x;
    private double _y;

    public Guid Id { get; } = Guid.NewGuid();

    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public double Width { get; internal set; }
    public double Height { get; internal set; }

    public IPortProvider? PortProvider { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

Note: `IPortProvider` won't exist yet — add a forward reference comment or create a stub interface. The build will fail until Task 5 adds it. **Alternative:** temporarily use `object?` for `PortProvider` and change the type in Task 5. **Better alternative:** create a minimal `IPortProvider` stub now:

Create `src/NodiumGraph/IPortProvider.cs` (stub, fleshed out in Task 5):

```csharp
using Avalonia;

namespace NodiumGraph;

public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position);
}
```

This won't compile until `Port` exists (Task 3). **Simplest path:** remove the `PortProvider` property from `Node` for now, add it in Task 5 after Port exists. Tests don't need it yet except `PortProvider_defaults_to_null` — skip that test for now, add it in Task 5.

**Revised:** Remove the `PortProvider` property and its test. They get added in Task 5.

**Step 4: Update PlaceholderTests**

Update `tests/NodiumGraph.Tests/PlaceholderTests.cs` to reference `Node` instead of deleted `INode`:

```csharp
using NodiumGraph;
using Xunit;

namespace NodiumGraph.Tests;

public class PlaceholderTests
{
    [Fact]
    public void Node_class_is_accessible()
    {
        var type = typeof(Node);
        Assert.NotNull(type);
    }
}
```

**Step 5: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~NodeTests"
```

Expected: 6 tests pass.

**Step 6: Commit**

```bash
git add src/NodiumGraph/Node.cs tests/NodiumGraph.Tests/NodeTests.cs tests/NodiumGraph.Tests/PlaceholderTests.cs
git rm src/NodiumGraph/INode.cs
git commit -m "Add Node class with INPC support"
```

---

### Task 3: Port class

**Files:**
- Create: `src/NodiumGraph/Port.cs`
- Create: `tests/NodiumGraph.Tests/PortTests.cs`

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/PortTests.cs`:

```csharp
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class PortTests
{
    [Fact]
    public void New_port_has_unique_id()
    {
        var node = new Node();
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(10, 10));
        Assert.NotEqual(port1.Id, port2.Id);
    }

    [Fact]
    public void Port_stores_owner_and_position()
    {
        var node = new Node();
        var port = new Port(node, new Point(5, 10));

        Assert.Same(node, port.Owner);
        Assert.Equal(new Point(5, 10), port.Position);
    }

    [Fact]
    public void AbsolutePosition_adds_owner_position()
    {
        var node = new Node { X = 100, Y = 200 };
        var port = new Port(node, new Point(10, 20));

        Assert.Equal(new Point(110, 220), port.AbsolutePosition);
    }

    [Fact]
    public void AbsolutePosition_updates_when_node_moves()
    {
        var node = new Node { X = 0, Y = 0 };
        var port = new Port(node, new Point(10, 10));

        Assert.Equal(new Point(10, 10), port.AbsolutePosition);

        node.X = 50;
        node.Y = 75;

        Assert.Equal(new Point(60, 85), port.AbsolutePosition);
    }

    [Fact]
    public void Port_requires_owner()
    {
        Assert.Throws<ArgumentNullException>(() => new Port(null!, new Point(0, 0)));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~PortTests"
```

Expected: FAIL — `Port` not defined.

**Step 3: Implement Port**

Create `src/NodiumGraph/Port.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public class Port
{
    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public Point Position { get; set; }

    public Point AbsolutePosition => new(Owner.X + Position.X, Owner.Y + Position.Y);

    public Port(Node owner, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Position = position;
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~PortTests"
```

Expected: 5 tests pass.

**Step 5: Commit**

```bash
git add src/NodiumGraph/Port.cs tests/NodiumGraph.Tests/PortTests.cs
git commit -m "Add Port class with AbsolutePosition computation"
```

---

### Task 4: Connection class

**Files:**
- Create: `src/NodiumGraph/Connection.cs`
- Create: `tests/NodiumGraph.Tests/ConnectionTests.cs`

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/ConnectionTests.cs`:

```csharp
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionTests
{
    [Fact]
    public void New_connection_has_unique_id()
    {
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 10));

        var conn1 = new Connection(source, target);
        var conn2 = new Connection(source, target);

        Assert.NotEqual(conn1.Id, conn2.Id);
    }

    [Fact]
    public void Connection_stores_source_and_target()
    {
        var nodeA = new Node();
        var nodeB = new Node();
        var source = new Port(nodeA, new Point(0, 0));
        var target = new Port(nodeB, new Point(0, 0));

        var conn = new Connection(source, target);

        Assert.Same(source, conn.SourcePort);
        Assert.Same(target, conn.TargetPort);
    }

    [Fact]
    public void Connection_requires_source()
    {
        var node = new Node();
        var target = new Port(node, new Point(0, 0));
        Assert.Throws<ArgumentNullException>(() => new Connection(null!, target));
    }

    [Fact]
    public void Connection_requires_target()
    {
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        Assert.Throws<ArgumentNullException>(() => new Connection(source, null!));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~ConnectionTests"
```

Expected: FAIL — `Connection` not defined.

**Step 3: Implement Connection**

Create `src/NodiumGraph/Connection.cs`:

```csharp
namespace NodiumGraph;

public class Connection
{
    public Guid Id { get; } = Guid.NewGuid();
    public Port SourcePort { get; }
    public Port TargetPort { get; }

    public Connection(Port sourcePort, Port targetPort)
    {
        SourcePort = sourcePort ?? throw new ArgumentNullException(nameof(sourcePort));
        TargetPort = targetPort ?? throw new ArgumentNullException(nameof(targetPort));
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~ConnectionTests"
```

Expected: 4 tests pass.

**Step 5: Commit**

```bash
git add src/NodiumGraph/Connection.cs tests/NodiumGraph.Tests/ConnectionTests.cs
git commit -m "Add Connection class"
```

---

### Task 5: IPortProvider, FixedPortProvider, and Node.PortProvider

**Files:**
- Create: `src/NodiumGraph/IPortProvider.cs`
- Create: `src/NodiumGraph/FixedPortProvider.cs`
- Modify: `src/NodiumGraph/Node.cs` — add PortProvider property
- Create: `tests/NodiumGraph.Tests/FixedPortProviderTests.cs`
- Modify: `tests/NodiumGraph.Tests/NodeTests.cs` — add PortProvider test

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/FixedPortProviderTests.cs`:

```csharp
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class FixedPortProviderTests
{
    [Fact]
    public void Ports_returns_declared_ports()
    {
        var node = new Node();
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(100, 0));

        var provider = new FixedPortProvider(new[] { port1, port2 });

        Assert.Equal(2, provider.Ports.Count);
        Assert.Contains(port1, provider.Ports);
        Assert.Contains(port2, provider.Ports);
    }

    [Fact]
    public void ResolvePort_returns_nearest_port_within_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(100, 0));

        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(5, 5));

        Assert.Same(port1, resolved);
    }

    [Fact]
    public void ResolvePort_returns_null_when_no_port_in_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));

        var provider = new FixedPortProvider(new[] { port1 });

        var resolved = provider.ResolvePort(new Point(500, 500));

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolvePort_picks_closest_when_multiple_in_radius()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));
        var port2 = new Port(node, new Point(20, 0));

        var provider = new FixedPortProvider(new[] { port1, port2 });

        var resolved = provider.ResolvePort(new Point(18, 0));

        Assert.Same(port2, resolved);
    }

    [Fact]
    public void Custom_hit_radius_is_respected()
    {
        var node = new Node { X = 0, Y = 0 };
        var port1 = new Port(node, new Point(0, 0));

        var provider = new FixedPortProvider(new[] { port1 }, hitRadius: 5.0);

        Assert.NotNull(provider.ResolvePort(new Point(4, 0)));
        Assert.Null(provider.ResolvePort(new Point(6, 0)));
    }

    [Fact]
    public void Empty_ports_list_is_valid()
    {
        var provider = new FixedPortProvider(Array.Empty<Port>());
        Assert.Empty(provider.Ports);
        Assert.Null(provider.ResolvePort(new Point(0, 0)));
    }
}
```

Add to `tests/NodiumGraph.Tests/NodeTests.cs`:

```csharp
    [Fact]
    public void PortProvider_defaults_to_null()
    {
        var node = new Node();
        Assert.Null(node.PortProvider);
    }
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~FixedPortProviderTests"
```

Expected: FAIL — types not defined.

**Step 3: Implement IPortProvider**

Create `src/NodiumGraph/IPortProvider.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position);
}
```

**Step 4: Implement FixedPortProvider**

Create `src/NodiumGraph/FixedPortProvider.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public class FixedPortProvider : IPortProvider
{
    private const double DefaultHitRadius = 20.0;
    private readonly double _hitRadius;

    public IReadOnlyList<Port> Ports { get; }

    public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = DefaultHitRadius)
    {
        Ports = ports.ToList().AsReadOnly();
        _hitRadius = hitRadius;
    }

    public Port? ResolvePort(Point position)
    {
        Port? closest = null;
        var closestDistance = double.MaxValue;

        foreach (var port in Ports)
        {
            var abs = port.AbsolutePosition;
            var dx = abs.X - position.X;
            var dy = abs.Y - position.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < _hitRadius && distance < closestDistance)
            {
                closest = port;
                closestDistance = distance;
            }
        }

        return closest;
    }
}
```

**Step 5: Add PortProvider to Node**

Add to `src/NodiumGraph/Node.cs`:

```csharp
public IPortProvider? PortProvider { get; set; }
```

**Step 6: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~FixedPortProviderTests|FullyQualifiedName~NodeTests"
```

Expected: All pass (6 FixedPortProvider + 7 Node including new PortProvider test).

**Step 7: Commit**

```bash
git add src/NodiumGraph/IPortProvider.cs src/NodiumGraph/FixedPortProvider.cs src/NodiumGraph/Node.cs tests/NodiumGraph.Tests/FixedPortProviderTests.cs tests/NodiumGraph.Tests/NodeTests.cs
git commit -m "Add IPortProvider interface and FixedPortProvider"
```

---

### Task 6: DynamicPortProvider

**Files:**
- Create: `src/NodiumGraph/DynamicPortProvider.cs`
- Create: `tests/NodiumGraph.Tests/DynamicPortProviderTests.cs`

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/DynamicPortProviderTests.cs`:

```csharp
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class DynamicPortProviderTests
{
    [Fact]
    public void Initially_has_no_ports()
    {
        var node = new Node { Width = 100, Height = 50 };
        var provider = new DynamicPortProvider(node);
        Assert.Empty(provider.Ports);
    }

    [Fact]
    public void ResolvePort_creates_port_at_boundary()
    {
        var node = new Node { X = 0, Y = 0 };
        // Width/Height are internal set — use reflection or a helper for tests
        SetNodeSize(node, 100, 50);

        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(110, 25));

        Assert.NotNull(port);
        Assert.Same(node, port!.Owner);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_reuses_existing_port_within_threshold()
    {
        var node = new Node { X = 0, Y = 0 };
        SetNodeSize(node, 100, 50);

        var provider = new DynamicPortProvider(node);

        var port1 = provider.ResolvePort(new Point(110, 25));
        var port2 = provider.ResolvePort(new Point(112, 26));

        Assert.Same(port1, port2);
        Assert.Single(provider.Ports);
    }

    [Fact]
    public void ResolvePort_creates_new_port_beyond_threshold()
    {
        var node = new Node { X = 0, Y = 0 };
        SetNodeSize(node, 100, 50);

        var provider = new DynamicPortProvider(node);

        var port1 = provider.ResolvePort(new Point(110, 0));
        var port2 = provider.ResolvePort(new Point(110, 50));

        Assert.NotSame(port1, port2);
        Assert.Equal(2, provider.Ports.Count);
    }

    [Fact]
    public void ResolvePort_returns_null_when_position_far_from_node()
    {
        var node = new Node { X = 0, Y = 0 };
        SetNodeSize(node, 100, 50);

        var provider = new DynamicPortProvider(node);

        var port = provider.ResolvePort(new Point(500, 500));

        Assert.Null(port);
    }

    /// Sets Width/Height which have internal set.
    /// Tests are in the same assembly via InternalsVisibleTo, or we use a test helper.
    private static void SetNodeSize(Node node, double width, double height)
    {
        node.Width = width;
        node.Height = height;
    }
}
```

Note: `Width`/`Height` are `internal set`. For tests to access them, add `InternalsVisibleTo` to the NodiumGraph project:

Add to `src/NodiumGraph/NodiumGraph.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="NodiumGraph.Tests" />
</ItemGroup>
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~DynamicPortProviderTests"
```

Expected: FAIL — `DynamicPortProvider` not defined.

**Step 3: Implement DynamicPortProvider**

Create `src/NodiumGraph/DynamicPortProvider.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public class DynamicPortProvider : IPortProvider
{
    private const double DefaultReuseThreshold = 15.0;
    private const double DefaultMaxDistance = 50.0;

    private readonly Node _owner;
    private readonly List<Port> _ports = new();
    private readonly double _reuseThreshold;
    private readonly double _maxDistance;

    public IReadOnlyList<Port> Ports => _ports.AsReadOnly();

    public DynamicPortProvider(Node owner, double reuseThreshold = DefaultReuseThreshold, double maxDistance = DefaultMaxDistance)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _reuseThreshold = reuseThreshold;
        _maxDistance = maxDistance;
    }

    public Port? ResolvePort(Point position)
    {
        var boundary = FindNearestBoundaryPoint(position);
        if (boundary is null)
            return null;

        // Check if an existing port is close enough to reuse
        foreach (var existing in _ports)
        {
            var abs = existing.AbsolutePosition;
            var dx = abs.X - boundary.Value.X;
            var dy = abs.Y - boundary.Value.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < _reuseThreshold)
                return existing;
        }

        // Create new port at boundary (relative to node)
        var relative = new Point(boundary.Value.X - _owner.X, boundary.Value.Y - _owner.Y);
        var port = new Port(_owner, relative);
        _ports.Add(port);
        return port;
    }

    private Point? FindNearestBoundaryPoint(Point position)
    {
        var left = _owner.X;
        var top = _owner.Y;
        var right = _owner.X + _owner.Width;
        var bottom = _owner.Y + _owner.Height;

        // Clamp position to boundary
        var clampedX = Math.Clamp(position.X, left, right);
        var clampedY = Math.Clamp(position.Y, top, bottom);

        // Snap to nearest edge
        var distLeft = Math.Abs(clampedX - left);
        var distRight = Math.Abs(clampedX - right);
        var distTop = Math.Abs(clampedY - top);
        var distBottom = Math.Abs(clampedY - bottom);

        var minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        Point boundaryPoint;
        if (minDist == distLeft)
            boundaryPoint = new Point(left, clampedY);
        else if (minDist == distRight)
            boundaryPoint = new Point(right, clampedY);
        else if (minDist == distTop)
            boundaryPoint = new Point(clampedX, top);
        else
            boundaryPoint = new Point(clampedX, bottom);

        // Check if position is within max distance from boundary
        var dx = position.X - boundaryPoint.X;
        var dy = position.Y - boundaryPoint.Y;
        if (Math.Sqrt(dx * dx + dy * dy) > _maxDistance)
            return null;

        return boundaryPoint;
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~DynamicPortProviderTests"
```

Expected: 5 tests pass.

**Step 5: Commit**

```bash
git add src/NodiumGraph/DynamicPortProvider.cs src/NodiumGraph/NodiumGraph.csproj tests/NodiumGraph.Tests/DynamicPortProviderTests.cs
git commit -m "Add DynamicPortProvider with boundary resolution and reuse"
```

---

### Task 7: Graph class

**Files:**
- Create: `src/NodiumGraph/Graph.cs`
- Create: `tests/NodiumGraph.Tests/GraphTests.cs`

**Step 1: Write failing tests**

Create `tests/NodiumGraph.Tests/GraphTests.cs`:

```csharp
using Avalonia;
using Xunit;

namespace NodiumGraph.Tests;

public class GraphTests
{
    [Fact]
    public void New_graph_has_empty_collections()
    {
        var graph = new Graph();
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Connections);
        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void AddNode_adds_to_nodes_collection()
    {
        var graph = new Graph();
        var node = new Node();

        graph.AddNode(node);

        Assert.Single(graph.Nodes);
        Assert.Contains(node, graph.Nodes);
    }

    [Fact]
    public void RemoveNode_removes_from_nodes_collection()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);

        graph.RemoveNode(node);

        Assert.Empty(graph.Nodes);
    }

    [Fact]
    public void RemoveNode_cascades_to_connected_connections()
    {
        var graph = new Graph();
        var nodeA = new Node();
        var nodeB = new Node();
        var portA = new Port(nodeA, new Point(0, 0));
        var portB = new Port(nodeB, new Point(0, 0));
        var connection = new Connection(portA, portB);

        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddConnection(connection);

        graph.RemoveNode(nodeA);

        Assert.Empty(graph.Connections);
        Assert.Single(graph.Nodes); // nodeB remains
    }

    [Fact]
    public void AddConnection_adds_to_connections_collection()
    {
        var graph = new Graph();
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var conn = new Connection(source, target);

        graph.AddConnection(conn);

        Assert.Single(graph.Connections);
    }

    [Fact]
    public void RemoveConnection_removes_from_connections_collection()
    {
        var graph = new Graph();
        var node = new Node();
        var source = new Port(node, new Point(0, 0));
        var target = new Port(node, new Point(10, 0));
        var conn = new Connection(source, target);
        graph.AddConnection(conn);

        graph.RemoveConnection(conn);

        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void RemoveNode_removes_from_selection()
    {
        var graph = new Graph();
        var node = new Node();
        graph.AddNode(node);
        // Select the node (SelectedNodes needs to be mutable internally)
        graph.Select(node);

        Assert.Single(graph.SelectedNodes);

        graph.RemoveNode(node);

        Assert.Empty(graph.SelectedNodes);
    }

    [Fact]
    public void AddNode_null_throws()
    {
        var graph = new Graph();
        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!));
    }

    [Fact]
    public void AddConnection_null_throws()
    {
        var graph = new Graph();
        Assert.Throws<ArgumentNullException>(() => graph.AddConnection(null!));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~GraphTests"
```

Expected: FAIL — `Graph` not defined.

**Step 3: Implement Graph**

Create `src/NodiumGraph/Graph.cs`:

```csharp
using System.Collections.ObjectModel;

namespace NodiumGraph;

public class Graph
{
    private readonly List<Node> _selectedNodes = new();

    public ObservableCollection<Node> Nodes { get; } = new();
    public ObservableCollection<Connection> Connections { get; } = new();
    public IReadOnlyList<Node> SelectedNodes => _selectedNodes.AsReadOnly();

    public void AddNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Nodes.Add(node);
    }

    public void RemoveNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Cascade: remove connections that reference this node's ports
        var toRemove = Connections
            .Where(c => c.SourcePort.Owner == node || c.TargetPort.Owner == node)
            .ToList();

        foreach (var conn in toRemove)
            Connections.Remove(conn);

        _selectedNodes.Remove(node);
        Nodes.Remove(node);
    }

    public void AddConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connections.Add(connection);
    }

    public void RemoveConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connections.Remove(connection);
    }

    public void Select(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_selectedNodes.Contains(node))
            _selectedNodes.Add(node);
    }

    public void Deselect(Node node)
    {
        _selectedNodes.Remove(node);
    }

    public void ClearSelection()
    {
        _selectedNodes.Clear();
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~GraphTests"
```

Expected: 9 tests pass.

**Step 5: Commit**

```bash
git add src/NodiumGraph/Graph.cs tests/NodiumGraph.Tests/GraphTests.cs
git commit -m "Add Graph class with cascading node removal"
```

---

### Task 8: Handler interfaces, strategy interfaces, supporting types

**Files:**
- Create: `src/NodiumGraph/NodeMoveInfo.cs`
- Create: `src/NodiumGraph/INodeInteractionHandler.cs`
- Create: `src/NodiumGraph/IConnectionHandler.cs`
- Create: `src/NodiumGraph/ISelectionHandler.cs`
- Create: `src/NodiumGraph/ICanvasInteractionHandler.cs`
- Create: `src/NodiumGraph/IConnectionValidator.cs`
- Create: `src/NodiumGraph/IConnectionRouter.cs`
- Create: `src/NodiumGraph/IConnectionStyle.cs`
- Create: `src/NodiumGraph/ConnectionStyle.cs`
- Create: `tests/NodiumGraph.Tests/ConnectionStyleTests.cs`

**Step 1: Write failing test for ConnectionStyle**

Create `tests/NodiumGraph.Tests/ConnectionStyleTests.cs`:

```csharp
using Avalonia.Media;
using Xunit;

namespace NodiumGraph.Tests;

public class ConnectionStyleTests
{
    [Fact]
    public void Default_style_has_sensible_defaults()
    {
        var style = new ConnectionStyle();

        Assert.NotNull(style.Stroke);
        Assert.True(style.Thickness > 0);
        Assert.Null(style.DashPattern);
    }

    [Fact]
    public void Custom_style_preserves_values()
    {
        var brush = Brushes.Red;
        var dash = DashStyle.Dash;

        var style = new ConnectionStyle(brush, 3.0, dash);

        Assert.Same(brush, style.Stroke);
        Assert.Equal(3.0, style.Thickness);
        Assert.Same(dash, style.DashPattern);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~ConnectionStyleTests"
```

Expected: FAIL — types not defined.

**Step 3: Implement all interfaces and types**

Create `src/NodiumGraph/NodeMoveInfo.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public record NodeMoveInfo(Node Node, Point OldPosition, Point NewPosition);
```

Create `src/NodiumGraph/INodeInteractionHandler.cs`:

```csharp
namespace NodiumGraph;

public interface INodeInteractionHandler
{
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);
    void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections);
    void OnNodeDoubleClicked(Node node);
}
```

Create `src/NodiumGraph/IConnectionHandler.cs`:

```csharp
namespace NodiumGraph;

public interface IConnectionHandler
{
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
```

Create `src/NodiumGraph/ISelectionHandler.cs`:

```csharp
namespace NodiumGraph;

public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyList<Node> selectedNodes);
}
```

Create `src/NodiumGraph/ICanvasInteractionHandler.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public interface ICanvasInteractionHandler
{
    void OnCanvasDoubleClicked(Point worldPosition);
    void OnCanvasDropped(Point worldPosition, object data);
}
```

Create `src/NodiumGraph/IConnectionValidator.cs`:

```csharp
namespace NodiumGraph;

public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
```

Create `src/NodiumGraph/IConnectionRouter.cs`:

```csharp
using Avalonia;

namespace NodiumGraph;

public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
}
```

Create `src/NodiumGraph/IConnectionStyle.cs`:

```csharp
using Avalonia.Media;

namespace NodiumGraph;

public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}
```

Create `src/NodiumGraph/ConnectionStyle.cs`:

```csharp
using Avalonia.Media;

namespace NodiumGraph;

public class ConnectionStyle : IConnectionStyle
{
    public IBrush Stroke { get; }
    public double Thickness { get; }
    public IDashStyle? DashPattern { get; }

    public ConnectionStyle(
        IBrush? stroke = null,
        double thickness = 2.0,
        IDashStyle? dashPattern = null)
    {
        Stroke = stroke ?? Brushes.Gray;
        Thickness = thickness;
        DashPattern = dashPattern;
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/NodiumGraph.Tests/ --filter "FullyQualifiedName~ConnectionStyleTests"
```

Expected: 2 tests pass.

**Step 5: Run full test suite**

```bash
dotnet test NodiumGraph.sln
```

Expected: All tests pass.

**Step 6: Commit**

```bash
git add src/NodiumGraph/ tests/NodiumGraph.Tests/ConnectionStyleTests.cs
git commit -m "Add handler interfaces, strategy interfaces, and ConnectionStyle"
```

---

### Task 9: Clean up and final verification

**Step 1: Delete placeholder tests**

Remove `tests/NodiumGraph.Tests/PlaceholderTests.cs` and `tests/NodiumGraph.Tests/CanvasPlaceholderTests.cs` — they're superseded by real tests.

**Step 2: Run full suite**

```bash
dotnet build NodiumGraph.sln
dotnet test NodiumGraph.sln
```

Expected: Build succeeds, all tests pass.

**Step 3: Commit**

```bash
git rm tests/NodiumGraph.Tests/PlaceholderTests.cs tests/NodiumGraph.Tests/CanvasPlaceholderTests.cs
git commit -m "Remove placeholder tests superseded by real tests"
```
