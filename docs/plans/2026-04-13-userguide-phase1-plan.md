# NodiumGraph User Guide — Phase 1 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:executing-plans to implement this plan task-by-task.

**Goal:** Land the user guide skeleton — all `docs/userguide/` Markdown files created with H1 + short intro + TODO markers, `index.md` with complete navigation, and a minimal `samples/GettingStarted/` Avalonia project added to the solution and building green.

**Architecture:** Diátaxis four-track layout (tutorial / how-to / reference / explanation). Plain Markdown, no frontmatter (end-user docs), normal relative links. Tutorial code is backed by a dedicated minimal `samples/GettingStarted/` project so docs-code drift shows up at `dotnet build` time. The existing `samples/NodiumGraph.Sample/` stays untouched — it remains the richer feature demo.

**Tech Stack:** Avalonia 12, .NET 10, Markdown, `dotnet build`.

**Scope:** Skeleton only. No content beyond H1 + one-sentence intro + "TODO: filled in Phase N" marker for each page. Sample contains enough code to host `NodiumGraphCanvas` and nothing else.

**Out of scope (later phases):** filling tutorial/reference/how-to/explanation content.

Reference: see `docs/plans/2026-04-13-userguide-design.md` for the full design and the 8-phase breakdown.

---

## Directory tree to create

```
docs/userguide/
  index.md
  tutorial/
    getting-started.md
  how-to/
    host-canvas.md
    custom-node-template.md
    bind-viewport.md
    handle-node-moves-undo.md
    external-drag-drop.md
    persist-graph-state.md
    custom-router.md
    custom-validator.md
    custom-style.md
    custom-port-provider.md
    subclass-model.md
    theme-canvas.md
    style-ports.md
    snap-to-grid.md
    configure-pan-zoom.md
    keyboard-shortcuts.md
  reference/
    canvas-control.md
    model.md
    handlers.md
    strategies.md
    result-pattern.md
    rendering-pipeline.md
  explanation/
    architecture.md
    report-dont-decide.md
    hybrid-rendering.md

samples/GettingStarted/
  GettingStarted.csproj
  Program.cs
  App.axaml
  App.axaml.cs
  MainWindow.axaml
  MainWindow.axaml.cs
```

---

## Skeleton page template

Every skeleton `.md` file (except `index.md`) follows this template. No frontmatter. No boilerplate beyond this.

```markdown
# <Human Page Title>

<One-sentence description of what this page will cover.>

> TODO: Content to be written in Phase <N> of the user guide plan.
```

Phase numbers per the design doc:
- Tutorial page → Phase 2
- Reference pages → Phase 3
- How-to (integration) → Phase 4
- How-to (extension) → Phase 5
- How-to (styling & interaction) → Phase 6
- Explanation pages → Phase 7

---

## Task 1: Create `docs/userguide/index.md`

**Files:**
- Create: `docs/userguide/index.md`

**Step 1: Write the file**

```markdown
# NodiumGraph User Guide

NodiumGraph is a graph editor library for Avalonia. It provides interactive canvas primitives — pan, zoom, node dragging, selection, and connection drawing — while leaving domain logic, styling, undo/redo, and serialization to your application.

This guide targets **Avalonia 12** and **.NET 10**.

## New here? Start with the tutorial

- [Getting Started](tutorial/getting-started.md) — Build your first NodiumGraph-based editor in about 30 minutes.

## How-to guides

Task-oriented recipes for specific problems. Each assumes you've read the tutorial or are already familiar with NodiumGraph basics.

**Consumer integration**

- [Host the canvas in a window or user control](how-to/host-canvas.md)
- [Define a custom node DataTemplate](how-to/custom-node-template.md)
- [Bind ViewportZoom, ViewportOffset, and SelectedNodes](how-to/bind-viewport.md)
- [Handle node moves for undo/redo](how-to/handle-node-moves-undo.md)
- [Handle external drag-drop onto the canvas](how-to/external-drag-drop.md)
- [Persist and restore graph state](how-to/persist-graph-state.md)

**Extension points**

- [Write a custom IConnectionRouter](how-to/custom-router.md)
- [Write a custom IConnectionValidator](how-to/custom-validator.md)
- [Write a custom IConnectionStyle](how-to/custom-style.md)
- [Write a custom IPortProvider](how-to/custom-port-provider.md)
- [Subclass Node and Connection for domain data](how-to/subclass-model.md)

**Styling and theming**

- [Theme the canvas](how-to/theme-canvas.md)
- [Style ports](how-to/style-ports.md)

**Interaction tweaks**

- [Enable snap-to-grid](how-to/snap-to-grid.md)
- [Configure pan and zoom gestures](how-to/configure-pan-zoom.md)
- [Add keyboard shortcuts](how-to/keyboard-shortcuts.md)

## Reference

Hand-curated reference for the consumer-facing API surface.

- [NodiumGraphCanvas control](reference/canvas-control.md) — styled properties, events, AXAML
- [Model classes](reference/model.md) — Node, Port, Connection, Graph
- [Handler interfaces](reference/handlers.md) — interaction callbacks
- [Strategy interfaces](reference/strategies.md) — router, validator, style, port provider
- [Result pattern](reference/result-pattern.md) — Error, Result, Result<T>
- [Rendering pipeline](reference/rendering-pipeline.md) — render order, coordinate spaces, hit-test order

## Explanation

Background on the library's design choices.

- [Architecture](explanation/architecture.md) — base classes and strategy interfaces
- [Report, don't decide](explanation/report-dont-decide.md) — the handler philosophy
- [Hybrid rendering](explanation/hybrid-rendering.md) — why nodes are controls but connections aren't

## What's not in NodiumGraph

These are intentionally **out of scope**. Your application is expected to own them:

- Undo/redo
- Serialization formats
- Automatic layout algorithms
- Keyboard shortcut defaults
- Connection validation rules (the library calls your validator; you decide)
- Node content and appearance (you provide the DataTemplate)

> TODO: Replace this TODO line once all linked pages have real content. Currently each linked page is a skeleton.
```

**Step 2: Commit**

```bash
git add docs/userguide/index.md
git commit -m "docs: add user guide landing page (skeleton)"
```

---

## Task 2: Create the tutorial skeleton

**Files:**
- Create: `docs/userguide/tutorial/getting-started.md`

**Step 1: Write the file**

```markdown
# Getting Started with NodiumGraph

Build a minimal Avalonia application that hosts `NodiumGraphCanvas`, displays a couple of nodes, and accepts connection drawing. About 30 minutes.

> TODO: Content to be written in Phase 2 of the user guide plan. This walkthrough will be backed end-to-end by the `samples/GettingStarted/` project so every code block is copied directly from compiled sources.
```

**Step 2: Commit**

```bash
git add docs/userguide/tutorial/getting-started.md
git commit -m "docs: add tutorial skeleton"
```

---

## Task 3: Create the how-to skeletons

**Files (create all 16 in one commit):**
- `docs/userguide/how-to/host-canvas.md`
- `docs/userguide/how-to/custom-node-template.md`
- `docs/userguide/how-to/bind-viewport.md`
- `docs/userguide/how-to/handle-node-moves-undo.md`
- `docs/userguide/how-to/external-drag-drop.md`
- `docs/userguide/how-to/persist-graph-state.md`
- `docs/userguide/how-to/custom-router.md`
- `docs/userguide/how-to/custom-validator.md`
- `docs/userguide/how-to/custom-style.md`
- `docs/userguide/how-to/custom-port-provider.md`
- `docs/userguide/how-to/subclass-model.md`
- `docs/userguide/how-to/theme-canvas.md`
- `docs/userguide/how-to/style-ports.md`
- `docs/userguide/how-to/snap-to-grid.md`
- `docs/userguide/how-to/configure-pan-zoom.md`
- `docs/userguide/how-to/keyboard-shortcuts.md`

**Step 1: Write each file** using the skeleton template. Use these H1s and one-liners verbatim (phase reference in parentheses):

| File | H1 | One-line intro |
|---|---|---|
| `host-canvas.md` | Host the Canvas in a Window or User Control | Add `NodiumGraphCanvas` to your Avalonia layout and bind it to a `Graph`. |
| `custom-node-template.md` | Define a Custom Node DataTemplate | Render your own node visuals with bindings to domain data. |
| `bind-viewport.md` | Bind ViewportZoom, ViewportOffset, and SelectedNodes | Two-way bind viewport state and selection to your view model. |
| `handle-node-moves-undo.md` | Handle Node Moves for Undo/Redo | Capture old and new positions from `INodeInteractionHandler` to feed an undo stack. |
| `external-drag-drop.md` | Handle External Drag-Drop onto the Canvas | Accept drops from outside the canvas and convert them to graph operations. |
| `persist-graph-state.md` | Persist and Restore Graph State | Guidance for serializing your graph (serialization format is your choice). |
| `custom-router.md` | Write a Custom IConnectionRouter | Return a point list for connection paths — bezier, orthogonal, elbow, or your own. |
| `custom-validator.md` | Write a Custom IConnectionValidator | Decide which source-target pairs are legal connections. |
| `custom-style.md` | Write a Custom IConnectionStyle | Data-drive connection stroke, thickness, and dash pattern. |
| `custom-port-provider.md` | Write a Custom IPortProvider | Control how ports appear on a node — fixed, dynamic, or custom. |
| `subclass-model.md` | Subclass Node and Connection for Domain Data | Attach your own fields to model classes without wrapping them. |
| `theme-canvas.md` | Theme the Canvas | Customize grid color, selection marquee, and background. |
| `style-ports.md` | Style Ports | Customize port size, shape, and hover / valid / invalid visual states. |
| `snap-to-grid.md` | Enable Snap-to-Grid | Turn on snap-to-grid and choose a grid size. |
| `configure-pan-zoom.md` | Configure Pan and Zoom Gestures | Clamp zoom, disable pan, or rebind pan keys. |
| `keyboard-shortcuts.md` | Add Keyboard Shortcuts | Wire Delete, Ctrl+A, and other shortcuts to graph operations. |

Each file's body follows the skeleton template: H1, the one-liner above as a paragraph, and:

```markdown
> TODO: Content to be written in Phase <N> of the user guide plan.
```

Phase number per the table at the top: host-canvas through persist-graph-state → Phase 4; custom-router through subclass-model → Phase 5; theme-canvas through keyboard-shortcuts → Phase 6.

**Step 2: Commit**

```bash
git add docs/userguide/how-to/
git commit -m "docs: add how-to guide skeletons"
```

---

## Task 4: Create the reference skeletons

**Files:**
- `docs/userguide/reference/canvas-control.md`
- `docs/userguide/reference/model.md`
- `docs/userguide/reference/handlers.md`
- `docs/userguide/reference/strategies.md`
- `docs/userguide/reference/result-pattern.md`
- `docs/userguide/reference/rendering-pipeline.md`

**Step 1: Write each file** using the skeleton template:

| File | H1 | One-line intro |
|---|---|---|
| `canvas-control.md` | NodiumGraphCanvas Control Reference | Styled properties, routed events, and AXAML usage for `NodiumGraphCanvas`. |
| `model.md` | Model Reference | Property and method reference for `Node`, `Port`, `Connection`, and `Graph`. |
| `handlers.md` | Handler Interfaces Reference | Interaction callback interfaces: when each method fires and what its return value means. |
| `strategies.md` | Strategy Interfaces Reference | Router, validator, style, and port-provider contracts plus built-in implementations. |
| `result-pattern.md` | Result Pattern Reference | `Error`, `Result`, and `Result<T>` — usage patterns and implicit operators. |
| `rendering-pipeline.md` | Rendering Pipeline Reference | Render order, coordinate spaces, hit-test order, and per-layer performance characteristics. |

All six pages use the skeleton template and reference Phase 3.

**Step 2: Commit**

```bash
git add docs/userguide/reference/
git commit -m "docs: add reference skeletons"
```

---

## Task 5: Create the explanation skeletons

**Files:**
- `docs/userguide/explanation/architecture.md`
- `docs/userguide/explanation/report-dont-decide.md`
- `docs/userguide/explanation/hybrid-rendering.md`

**Step 1: Write each file** using the skeleton template:

| File | H1 | One-line intro |
|---|---|---|
| `architecture.md` | Architecture: Base Classes and Strategy Interfaces | Why NodiumGraph uses concrete base model classes combined with strategy interfaces for extensibility. |
| `report-dont-decide.md` | Report, Don't Decide | Why interaction handlers report events instead of mutating the graph, and what this means for undo/redo. |
| `hybrid-rendering.md` | Hybrid Rendering | Why nodes are real Avalonia controls but connections, grid, and overlays are custom-rendered. |

All three reference Phase 7.

**Step 2: Commit**

```bash
git add docs/userguide/explanation/
git commit -m "docs: add explanation skeletons"
```

---

## Task 6: Scaffold the GettingStarted sample project

**Files:**
- Create: `samples/GettingStarted/GettingStarted.csproj`
- Create: `samples/GettingStarted/Program.cs`
- Create: `samples/GettingStarted/App.axaml`
- Create: `samples/GettingStarted/App.axaml.cs`
- Create: `samples/GettingStarted/MainWindow.axaml`
- Create: `samples/GettingStarted/MainWindow.axaml.cs`

Mirror the existing `samples/NodiumGraph.Sample/NodiumGraph.Sample.csproj` structure. Central package management is handled by `Directory.Packages.props` at the repo root, so no explicit versions are needed.

**Step 1: Write `GettingStarted.csproj`**

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
    <ProjectReference Include="..\..\src\NodiumGraph\NodiumGraph.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Write `Program.cs`**

```csharp
using Avalonia;

namespace GettingStarted;

internal static class Program
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

**Step 3: Write `App.axaml`**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="GettingStarted.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

**Step 4: Write `App.axaml.cs`**

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace GettingStarted;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

**Step 5: Write `MainWindow.axaml`**

The skeleton sample hosts `NodiumGraphCanvas` with no content so it builds and runs but is intentionally blank. Real content is filled in Phase 2.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ng="clr-namespace:NodiumGraph;assembly=NodiumGraph"
        x:Class="GettingStarted.MainWindow"
        Title="NodiumGraph — Getting Started"
        Width="1024" Height="720">
  <ng:NodiumGraphCanvas />
</Window>
```

> **Gotcha:** If the `xmlns:ng` assembly reference fails at build time, double-check the `NodiumGraph` root namespace matches the `assembly=` attribute. The project uses `namespace NodiumGraph`; no subnamespace needed.

**Step 6: Write `MainWindow.axaml.cs`**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GettingStarted;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
```

**Step 7: Do NOT commit yet** — Task 7 adds the project to the solution and Task 8 verifies it builds. Commit once in Task 8.

---

## Task 7: Add GettingStarted to the solution

**Files:**
- Modify: `NodiumGraph.sln`

**Step 1: Add the project to the solution**

Run from the repo root:

```bash
dotnet sln NodiumGraph.sln add samples/GettingStarted/GettingStarted.csproj --solution-folder samples
```

Expected output: `Project '...GettingStarted.csproj' added to the solution.`

**Step 2: Verify the project appears in the samples solution folder**

```bash
dotnet sln NodiumGraph.sln list
```

Expected output includes both `samples\NodiumGraph.Sample\NodiumGraph.Sample.csproj` and `samples\GettingStarted\GettingStarted.csproj`.

---

## Task 8: Verify the build and commit the sample

**Step 1: Build the solution**

```bash
dotnet build NodiumGraph.sln
```

Expected: `Build succeeded`. Zero errors. Any warnings in the new project must be addressed before commit.

**Step 2: (Optional smoke run — skip if no display available)**

```bash
dotnet run --project samples/GettingStarted/GettingStarted.csproj
```

Expected: A window opens titled "NodiumGraph — Getting Started" with an empty canvas. Close the window. This step is optional because some environments lack a display; the build-succeeded check in Step 1 is the authoritative gate.

**Step 3: Commit**

```bash
git add samples/GettingStarted/ NodiumGraph.sln
git commit -m "samples: add minimal GettingStarted project for user guide tutorial"
```

---

## Task 9: Final verification

**Step 1: Confirm the directory tree**

```bash
find docs/userguide -type f -name '*.md' | sort
```

Expected: 26 files — 1 index + 1 tutorial + 16 how-to + 6 reference + 3 explanation. Count:

```bash
find docs/userguide -type f -name '*.md' | wc -l
```

Expected: `26`.

**Step 2: Confirm no dead internal links in `index.md`**

```bash
grep -oE '\(([a-zA-Z0-9/_-]+\.md)\)' docs/userguide/index.md | sed -E 's/[()]//g' | while read link; do
  test -f "docs/userguide/$link" && echo "OK  $link" || echo "MISSING $link"
done
```

Expected: every line starts with `OK`. No `MISSING`.

**Step 3: Confirm build still green**

```bash
dotnet build NodiumGraph.sln
```

Expected: `Build succeeded`.

**Step 4: Check git status is clean**

```bash
git status
```

Expected: `nothing to commit, working tree clean`. Six commits ahead of origin/main from this phase (index, tutorial, how-to, reference, explanation, sample).

No additional commit in this task — if any fix-ups were needed they belong in their originating task's commit (amend-before-push is fine here since these commits have not been pushed).

---

## Phase 1 Definition of Done

- [ ] `docs/userguide/` contains 26 skeleton `.md` files in the correct Diátaxis layout
- [ ] `docs/userguide/index.md` links to every skeleton page with no broken links
- [ ] `samples/GettingStarted/` builds from a clean `dotnet build`
- [ ] `GettingStarted.csproj` is referenced from `NodiumGraph.sln` under the `samples` solution folder
- [ ] Six commits landed on the branch (one per task cluster)
- [ ] `git status` clean, tree green

Phase 2 (tutorial content against the sample) picks up from this state.
