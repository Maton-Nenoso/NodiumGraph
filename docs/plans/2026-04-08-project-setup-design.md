# NodiumGraph Project Setup Design

**Date:** 2026-04-08
**Status:** Approved

## Goal

Set up the NodiumGraph solution skeleton: compilable projects, green tests, launchable sample app. No library implementation beyond stubs.

## Decisions

| Decision | Choice |
|---|---|
| Target framework | .NET 10 (`net10.0`) |
| UI framework | Avalonia 12.0.0 |
| Test framework | xUnit |
| Package management | Central Package Management (CPM) |
| Solution layout | `src/` + `tests/` + `samples/` with `Directory.Build.props` |

## Solution Structure

```
NodiumGraph/
├── NodiumGraph.sln
├── Directory.Build.props
├── Directory.Packages.props
├── src/
│   ├── NodiumGraph.Core/
│   └── NodiumGraph.Avalonia/
├── samples/
│   └── NodiumGraph.Sample/
├── tests/
│   ├── NodiumGraph.Core.Tests/
│   └── NodiumGraph.Avalonia.Tests/
└── docs/
```

## Directory.Build.props

Shared settings across all projects:
- `TargetFramework`: `net10.0`
- `LangVersion`: `latest`
- `Nullable`: `enable`
- `ImplicitUsings`: `enable`

## Directory.Packages.props (CPM)

| Package | Version | Used by |
|---|---|---|
| Avalonia | 12.0.0 | Avalonia, Sample, Avalonia.Tests |
| Avalonia.Desktop | 12.0.0 | Sample |
| Avalonia.Themes.Fluent | 12.0.0 | Sample, Avalonia.Tests |
| Avalonia.Headless.XUnit | 12.0.0 | Avalonia.Tests |
| Microsoft.NET.Test.Sdk | latest | both test projects |
| xunit | latest | both test projects |
| xunit.runner.visualstudio | latest | both test projects |

## Projects

### NodiumGraph.Core
- Class library, zero NuGet dependencies
- Contains placeholder files for: INode, IPort, IPortProvider, IConnection, IConnectionValidator, IConnectionRouter, IConnectionStyle, handler interfaces, NodeMoveInfo record

### NodiumGraph.Avalonia
- Class library, depends on Avalonia + project ref to Core
- Contains empty NodiumGraphCanvas TemplatedControl stub

### NodiumGraph.Sample
- Avalonia Desktop executable, depends on Avalonia.Desktop, Avalonia.Themes.Fluent, project ref to Avalonia
- Minimal App.axaml with FluentTheme, MainWindow.axaml with empty canvas placeholder
- Just enough to launch a window

### NodiumGraph.Core.Tests
- xUnit, project ref to Core
- Single placeholder test that passes

### NodiumGraph.Avalonia.Tests
- xUnit + Avalonia.Headless.XUnit, project ref to Avalonia
- App.axaml + TestAppBuilder for headless host
- Single placeholder [AvaloniaFact] test that passes

## Scope Boundary

This design covers skeleton setup only. Library implementation (interfaces, canvas control, rendering) follows in subsequent plans.
