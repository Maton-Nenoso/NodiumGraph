---
title: NodiumGraph User Guide — Design
tags: [plan, userguide]
status: active
created: 2026-04-13
updated: 2026-04-13
---

# NodiumGraph User Guide — Design

## Goal

Ship a complete, hand-authored user guide for NodiumGraph consumers: tutorial, how-to recipes, API/AXAML reference, and explanation essays. Target audience is a .NET developer with mixed Avalonia experience who wants to embed a graph editor into an Avalonia application.

## Audience assumptions

- Mixed Avalonia familiarity. Tutorial includes brief Avalonia primers (DataTemplate, styled properties, etc.) where relevant. Reference and how-to assume fluency.
- C# and MVVM fluency is assumed.
- Targets Avalonia 12 / .NET 10. Every page that uses Avalonia APIs states the version.

## Non-goals

- No auto-generated API reference (DocFX, Sandcastle, etc.). Hand-curated tables only.
- No undo/redo, serialization, layout algorithms, keyboard-shortcut catalog — these are consumer responsibility and are explicitly listed as out of scope on `index.md`.
- No exhaustive dump of every inherited Avalonia member. Reference pages document the NodiumGraph surface only.

## Track: Diátaxis

The guide uses Diátaxis's four tracks: *Tutorial* (learning), *How-to* (task), *Reference* (information), *Explanation* (understanding). Each page lives under its track folder; cross-links between tracks are normal Markdown relative links.

## Folder layout

```
docs/
  userguide/
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
samples/
  GettingStarted/
    GettingStarted.csproj
    App.axaml
    App.axaml.cs
    MainWindow.axaml
    MainWindow.axaml.cs
    Program.cs
```

## Authoring conventions

- **No YAML frontmatter.** The user guide is end-user-facing; pages start directly with an H1.
- **Normal relative Markdown links** between files (`../reference/handlers.md`), not wikilinks.
- **Screenshot placeholders.** Where a screenshot would help, include a fenced placeholder block describing the intended content:
  ```
  > **Screenshot:** The NodiumGraph canvas showing two nodes connected by a bezier curve, one node selected with a blue marquee outline, grid visible at 20px spacing.
  ```
  These can be replaced with real images later without restructuring.
- **Language-tagged code blocks** (```csharp, ```xml for AXAML).
- **Snippets from the sample project** get a first-line comment: `// from: samples/GettingStarted/MainWindow.axaml.cs`.
- **Diagrams** use Mermaid or ASCII art where a diagram genuinely clarifies (render order, coordinate spaces, handler flow).
- **XML doc comments** remain the authoritative short description in code; reference pages expand on them with usage context, gotchas, and examples.

## Track content

### Tutorial — `tutorial/getting-started.md`

One end-to-end ~30-minute walkthrough backed by `samples/GettingStarted/`:

1. Install NuGet package, create Avalonia app (or add to existing one).
2. Add `NodiumGraphCanvas` to `MainWindow.axaml`.
3. Create a `Graph` with two hard-coded nodes in the view model.
4. Define a node DataTemplate with a simple header and ports.
5. Wire `INodeInteractionHandler` to accept drags (demonstrates *report, don't decide*).
6. Wire `IConnectionHandler.OnConnectionRequested` to accept connections.
7. Add a custom `IConnectionValidator` forbidding self-connections.
8. "Where next" — links into how-to and explanation tracks.

Every code block is copied from `samples/GettingStarted/` so it cannot drift.

### How-to recipes

Each follows a strict template: *Goal*, *Prerequisites*, *Steps*, *Full code*, *Gotchas*, *See also*.

**Consumer integration (6):**
1. `host-canvas.md` — Host `NodiumGraphCanvas` in a window / user control
2. `custom-node-template.md` — Define a custom node DataTemplate with bindings, ports, styling
3. `bind-viewport.md` — Two-way bind `ViewportZoom`, `ViewportOffset`, `SelectedNodes`
4. `handle-node-moves-undo.md` — Handle node moves for undo/redo using old/new positions
5. `external-drag-drop.md` — Handle external drag-drop onto the canvas
6. `persist-graph-state.md` — Pointer recipe for serialization (serialization is out of scope, but guide shows where the seams are)

**Extension points (5):**
7. `custom-router.md` — Implement `IConnectionRouter` (bezier / orthogonal / elbow)
8. `custom-validator.md` — Implement `IConnectionValidator` with type-compatibility rules
9. `custom-style.md` — Implement `IConnectionStyle` for data-driven stroke/dash
10. `custom-port-provider.md` — Implement `IPortProvider` for dynamic port creation
11. `subclass-model.md` — Subclass `Node` / `Connection` for domain data

**Styling & theming (2):**
12. `theme-canvas.md` — Theme grid color, selection marquee, background
13. `style-ports.md` — Style port size, shape, hover/valid/invalid states

**Interaction tweaks (3):**
14. `snap-to-grid.md` — Enable snap-to-grid
15. `configure-pan-zoom.md` — Configure pan/zoom gestures (disable, clamp, custom keys)
16. `keyboard-shortcuts.md` — Add consumer-side shortcuts (delete, select-all)

### Reference

Hand-curated tables focused on the consumer-facing surface.

- **`canvas-control.md`** — `NodiumGraphCanvas` styled-property table (name / type / default / description), routed events, complete AXAML usage example.
- **`model.md`** — `Node`, `Port`, `Connection`, `Graph` — property tables, INPC behavior, collection semantics, mutation rules.
- **`handlers.md`** — `INodeInteractionHandler`, `IConnectionHandler`, `ISelectionHandler`, `ICanvasInteractionHandler` — signatures, firing conditions, return-value semantics, threading.
- **`strategies.md`** — `IConnectionRouter`, `IConnectionValidator`, `IConnectionStyle`, `IPortProvider` — contracts, invocation timing, built-in implementations (`FixedPortProvider`, `DynamicPortProvider`, `DefaultConnectionValidator`, `ConnectionStyle`).
- **`result-pattern.md`** — `Error`, `Result`, `Result<T>` — usage patterns, implicit operators, anti-patterns.
- **`rendering-pipeline.md`** — render order as a table, coordinate-space diagram (world vs screen vs control), hit-test order, per-layer perf characteristics.

### Explanation (3 short essays, 500–900 words)

- **`architecture.md`** — Why base classes + strategy interfaces instead of pure DI or pure virtuals.
- **`report-dont-decide.md`** — The handler philosophy, worked example of why reporting (not mutating) is critical for undo/redo.
- **`hybrid-rendering.md`** — Why nodes are real Avalonia controls but connections/grid are custom-rendered.

## Samples project

Single `samples/GettingStarted/` Avalonia app. The tutorial's code blocks are copied verbatim from this project; how-to recipes cite snippets where applicable. The project is added to the solution so CI builds it and catches drift.

## Delivery phases

Each phase is its own commit (or small cluster of commits). Phases run in order; later phases may uncover small edits to earlier ones.

1. **Skeleton** — all userguide `.md` files created with H1 + brief intro + TODO markers. `index.md` has the full navigation tree. `samples/GettingStarted/` scaffolded and building. Solution reference added.
2. **Tutorial + sample** — fill `tutorial/getting-started.md` against the working sample.
3. **Reference** — the six reference pages.
4. **How-to: consumer integration** — recipes 1–6.
5. **How-to: extension points** — recipes 7–11.
6. **How-to: styling & interaction** — recipes 12–16.
7. **Explanation essays** — the three essays.
8. **Polish** — cross-links, "see also" sections, consistency pass, dead-link check.

## Open risks

- **API drift risk in Reference.** Mitigated by (a) hand-curated tables focused on small surface, (b) XML doc comment source-of-truth discipline, (c) tutorial code backed by compiled sample. If the surface triples, reconsider DocFX.
- **Screenshot debt.** Placeholders are explicit so they can be filled in later. No missing visuals should block v1.
- **Sample-project bitrot.** Mitigated by adding it to the solution so `dotnet build` catches breakage.

## Next step

Invoke the `writing-plans` skill to produce an executable implementation plan for Phase 1 (skeleton + sample scaffold).
