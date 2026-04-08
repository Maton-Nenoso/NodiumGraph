# NodiumGraph Project Setup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Create a compilable NodiumGraph solution skeleton with five projects, green tests, and a launchable sample app.

**Architecture:** Two library projects (Core with zero deps, Avalonia referencing Core), one sample desktop app, two test projects (pure xUnit for Core, headless Avalonia xUnit for Avalonia). Central Package Management for version pinning.

**Tech Stack:** .NET 10, Avalonia 12.0.0, xUnit 2.9.3, Avalonia.Headless.XUnit

---

### Task 0: Create solution and shared build configuration

**Files:**
- Create: `NodiumGraph.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`

**Step 1: Create the solution file**

Run:
```bash
cd D:/Projects/Nenoso/NodiumGraph
dotnet new sln --name NodiumGraph
```

**Step 2: Create Directory.Build.props**

Create `Directory.Build.props` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**Step 3: Create Directory.Packages.props**

Create `Directory.Packages.props` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Avalonia -->
    <PackageVersion Include="Avalonia" Version="12.0.0" />
    <PackageVersion Include="Avalonia.Desktop" Version="12.0.0" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="12.0.0" />
    <PackageVersion Include="Avalonia.Headless.XUnit" Version="12.0.0" />
    <!-- Testing -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.4.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

**Step 4: Commit**

```bash
git add NodiumGraph.sln Directory.Build.props Directory.Packages.props
git commit -m "Add solution file and shared build configuration"
```

---

### Task 1: Create NodiumGraph.Core class library

**Files:**
- Create: `src/NodiumGraph.Core/NodiumGraph.Core.csproj`
- Create: `src/NodiumGraph.Core/INode.cs`

**Step 1: Create the project**

```bash
mkdir -p src/NodiumGraph.Core
```

Create `src/NodiumGraph.Core/NodiumGraph.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

Note: `TargetFramework`, `LangVersion`, `Nullable`, `ImplicitUsings` all come from `Directory.Build.props`. No PackageReferences needed — Core has zero dependencies.

**Step 2: Add a placeholder interface**

Create `src/NodiumGraph.Core/INode.cs`:

```csharp
namespace NodiumGraph.Core;

/// <summary>
/// Represents a node in the graph. Implemented by consumer types.
/// </summary>
public interface INode
{
    Guid Id { get; }
    double X { get; set; }
    double Y { get; set; }
    double Width { get; }
    double Height { get; }
}
```

**Step 3: Add project to solution and build**

```bash
dotnet sln add src/NodiumGraph.Core/NodiumGraph.Core.csproj
dotnet build src/NodiumGraph.Core/NodiumGraph.Core.csproj
```

Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/NodiumGraph.Core/ NodiumGraph.sln
git commit -m "Add NodiumGraph.Core class library with INode interface"
```

---

### Task 2: Create NodiumGraph.Avalonia class library

**Files:**
- Create: `src/NodiumGraph.Avalonia/NodiumGraph.Avalonia.csproj`
- Create: `src/NodiumGraph.Avalonia/NodiumGraphCanvas.cs`

**Step 1: Create the project**

```bash
mkdir -p src/NodiumGraph.Avalonia
```

Create `src/NodiumGraph.Avalonia/NodiumGraph.Avalonia.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Avalonia" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\NodiumGraph.Core\NodiumGraph.Core.csproj" />
  </ItemGroup>

</Project>
```

Note: No `Version` attribute on PackageReference — CPM provides it.

**Step 2: Add a placeholder canvas control**

Create `src/NodiumGraph.Avalonia/NodiumGraphCanvas.cs`:

```csharp
using Avalonia.Controls.Primitives;

namespace NodiumGraph.Avalonia;

/// <summary>
/// The primary graph editor canvas control.
/// </summary>
public class NodiumGraphCanvas : TemplatedControl
{
}
```

**Step 3: Add to solution and build**

```bash
dotnet sln add src/NodiumGraph.Avalonia/NodiumGraph.Avalonia.csproj
dotnet build src/NodiumGraph.Avalonia/NodiumGraph.Avalonia.csproj
```

Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/NodiumGraph.Avalonia/ NodiumGraph.sln
git commit -m "Add NodiumGraph.Avalonia class library with NodiumGraphCanvas stub"
```

---

### Task 3: Create NodiumGraph.Sample desktop app

**Files:**
- Create: `samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj`
- Create: `samples/NodiumGraph.Sample/Program.cs`
- Create: `samples/NodiumGraph.Sample/App.axaml`
- Create: `samples/NodiumGraph.Sample/App.axaml.cs`
- Create: `samples/NodiumGraph.Sample/MainWindow.axaml`
- Create: `samples/NodiumGraph.Sample/MainWindow.axaml.cs`

**Step 1: Create the project**

```bash
mkdir -p samples/NodiumGraph.Sample
```

Create `samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\NodiumGraph.Avalonia\NodiumGraph.Avalonia.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create Program.cs**

Create `samples/NodiumGraph.Sample/Program.cs`:

```csharp
using Avalonia;

namespace NodiumGraph.Sample;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

**Step 3: Create App.axaml**

Create `samples/NodiumGraph.Sample/App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="NodiumGraph.Sample.App">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

**Step 4: Create App.axaml.cs**

Create `samples/NodiumGraph.Sample/App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Markup.Xaml;

namespace NodiumGraph.Sample;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
```

**Step 5: Create MainWindow.axaml**

Create `samples/NodiumGraph.Sample/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="NodiumGraph.Sample.MainWindow"
        Title="NodiumGraph Sample"
        Width="1024" Height="768">
    <TextBlock Text="NodiumGraph canvas will go here."
               HorizontalAlignment="Center"
               VerticalAlignment="Center" />
</Window>
```

**Step 6: Create MainWindow.axaml.cs**

Create `samples/NodiumGraph.Sample/MainWindow.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace NodiumGraph.Sample;

public partial class MainWindow : Window
{
}
```

**Step 7: Add to solution and build**

```bash
dotnet sln add samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj
dotnet build samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj
```

Expected: Build succeeded.

**Step 8: Verify the sample launches**

```bash
dotnet run --project samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj &
```

Expected: A window titled "NodiumGraph Sample" appears. Close it manually.

**Step 9: Commit**

```bash
git add samples/NodiumGraph.Sample/ NodiumGraph.sln
git commit -m "Add NodiumGraph.Sample desktop app with Fluent theme"
```

---

### Task 4: Create NodiumGraph.Core.Tests

**Files:**
- Create: `tests/NodiumGraph.Core.Tests/NodiumGraph.Core.Tests.csproj`
- Create: `tests/NodiumGraph.Core.Tests/PlaceholderTests.cs`

**Step 1: Create the project**

```bash
mkdir -p tests/NodiumGraph.Core.Tests
```

Create `tests/NodiumGraph.Core.Tests/NodiumGraph.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\NodiumGraph.Core\NodiumGraph.Core.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Write a placeholder test**

Create `tests/NodiumGraph.Core.Tests/PlaceholderTests.cs`:

```csharp
using NodiumGraph.Core;

namespace NodiumGraph.Core.Tests;

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

**Step 3: Add to solution and run tests**

```bash
dotnet sln add tests/NodiumGraph.Core.Tests/NodiumGraph.Core.Tests.csproj
dotnet test tests/NodiumGraph.Core.Tests/NodiumGraph.Core.Tests.csproj
```

Expected: 1 test passed.

**Step 4: Commit**

```bash
git add tests/NodiumGraph.Core.Tests/ NodiumGraph.sln
git commit -m "Add NodiumGraph.Core.Tests with xUnit placeholder test"
```

---

### Task 5: Create NodiumGraph.Avalonia.Tests with headless setup

**Files:**
- Create: `tests/NodiumGraph.Avalonia.Tests/NodiumGraph.Avalonia.Tests.csproj`
- Create: `tests/NodiumGraph.Avalonia.Tests/App.axaml`
- Create: `tests/NodiumGraph.Avalonia.Tests/App.axaml.cs`
- Create: `tests/NodiumGraph.Avalonia.Tests/TestAppBuilder.cs`
- Create: `tests/NodiumGraph.Avalonia.Tests/PlaceholderTests.cs`

**Step 1: Create the project**

```bash
mkdir -p tests/NodiumGraph.Avalonia.Tests
```

Create `tests/NodiumGraph.Avalonia.Tests/NodiumGraph.Avalonia.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Headless.XUnit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\NodiumGraph.Avalonia\NodiumGraph.Avalonia.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create App.axaml**

Create `tests/NodiumGraph.Avalonia.Tests/App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="NodiumGraph.Avalonia.Tests.App">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

**Step 3: Create App.axaml.cs**

Create `tests/NodiumGraph.Avalonia.Tests/App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Markup.Xaml;

namespace NodiumGraph.Avalonia.Tests;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
```

**Step 4: Create TestAppBuilder.cs**

Create `tests/NodiumGraph.Avalonia.Tests/TestAppBuilder.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(NodiumGraph.Avalonia.Tests.TestAppBuilder))]

namespace NodiumGraph.Avalonia.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

**Step 5: Write a placeholder headless test**

Create `tests/NodiumGraph.Avalonia.Tests/PlaceholderTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace NodiumGraph.Avalonia.Tests;

public class PlaceholderTests
{
    [AvaloniaFact]
    public void NodiumGraphCanvas_can_be_created()
    {
        var canvas = new NodiumGraph.Avalonia.NodiumGraphCanvas();
        var window = new Window { Content = canvas };
        window.Show();

        Assert.NotNull(canvas);
    }
}
```

**Step 6: Add to solution and run tests**

```bash
dotnet sln add tests/NodiumGraph.Avalonia.Tests/NodiumGraph.Avalonia.Tests.csproj
dotnet test tests/NodiumGraph.Avalonia.Tests/NodiumGraph.Avalonia.Tests.csproj
```

Expected: 1 test passed.

**Step 7: Commit**

```bash
git add tests/NodiumGraph.Avalonia.Tests/ NodiumGraph.sln
git commit -m "Add NodiumGraph.Avalonia.Tests with headless xUnit setup"
```

---

### Task 6: Full solution build and test verification

**Step 1: Build entire solution**

```bash
dotnet build NodiumGraph.sln
```

Expected: Build succeeded, 0 warnings, 0 errors.

**Step 2: Run all tests**

```bash
dotnet test NodiumGraph.sln
```

Expected: 2 tests passed (1 Core, 1 Avalonia).

**Step 3: Commit any remaining changes**

If all clean, no commit needed.

**Step 4: Final commit — remove stale codex.md if not yet committed**

```bash
git status
```

Check for any unstaged changes and commit if needed.
