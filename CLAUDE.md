# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NodiumGraph is an open-source graph editor library for **Avalonia** (.NET). It provides interactive canvas primitives — pan, zoom, node dragging, selection, and connection drawing — while leaving domain logic, styling, undo/redo, and serialization to the consumer.

- **Owner:** Maton-Nenoso (Stefan Maton)
- **License:** MIT
- **Target:** Avalonia 12, .NET 10, AOT-compatible, zero third-party deps beyond Avalonia
- **Release status:** pre-1.0, no public users. Breaking changes are free — do not add backwards-compatibility shims, deprecation wrappers, or migration notes. Pick the right design now. This policy flips the moment we ship 1.0.

## Build & Test Commands

- `dotnet build` — build the solution
- `dotnet test` — run tests
- IDE: Rider or Visual Studio

## Package Structure

```
NodiumGraph   — Single library: model classes, interfaces, canvas control, rendering, interaction handling.
```

Single project, single namespace (`NodiumGraph`). References Avalonia directly — this is an Avalonia extension library.

## Architecture

### Design Principles

1. **Base classes + strategy interfaces** — Concrete base classes (Node, Port, Connection, Graph) for the model. Interfaces for consumer-implemented strategies (routing, validation, styling) and interaction handlers.
2. **Hybrid rendering** — Nodes are real Avalonia controls (DataTemplate-driven). Connections, grid, and canvas chrome are custom-rendered for performance.
3. **Report, don't decide** — The library reports interactions (node moved, connection requested, delete pressed) via handler interfaces. The consumer decides what to do. The library never mutates domain state directly.
4. **Small surface area** — Ship primitives only. Routing algorithms, layout engines, serialization live outside.

### Model Classes (concrete, unsealed)

- **`Node`** — Id, X, Y, Width (internal set), Height (internal set), PortProvider. Implements INotifyPropertyChanged. Subclassable for domain data.
- **`Port`** — Id, Owner, Position (relative to node), AbsolutePosition (computed). Connection endpoint.
- **`Connection`** — Id, SourcePort, TargetPort. Subclassable for labels/weights.
- **`Graph`** — ObservableCollection<Node> Nodes, ObservableCollection<Connection> Connections, SelectedNodes. AddNode/RemoveNode (cascades to connections), AddConnection/RemoveConnection.

### Port Provider Strategy

- **`IPortProvider`** — Ports list + ResolvePort(Point) method. Set per node instance.
- **`FixedPortProvider`** — Declared ports at fixed positions. ResolvePort returns nearest within radius.
- **`DynamicPortProvider`** — Creates ports at boundary intersection. Reuses existing ports within distance threshold.

### Result Pattern

- **`Error`** — record with Message, Code.
- **`Result`** / **`Result<T>`** — Success/Failure with implicit operators. Used by handlers (e.g., `OnConnectionRequested` returns `Result<Connection>`).

### Interaction Handlers

All optional (nullable properties on canvas). The library functions with defaults when not provided.

- **`INodeInteractionHandler`** — `OnNodesMoved` (with old/new positions for undo), `OnDeleteRequested`, `OnNodeDoubleClicked`
- **`IConnectionHandler`** — `OnConnectionRequested` → `Result<Connection>`, `OnConnectionDeleteRequested`
- **`ISelectionHandler`** — `OnSelectionChanged`
- **`ICanvasInteractionHandler`** — `OnCanvasDoubleClicked`, `OnCanvasDropped` (external drag-drop)

### Strategy Interfaces

- **`IConnectionValidator`** — `CanConnect(source, target)` called during drag for accept/reject feedback.
- **`IConnectionRouter`** — Returns point list for connection path. Consumer implements bezier, orthogonal, etc.
- **`IConnectionStyle`** — Per-connection stroke, thickness, dash pattern. Default `ConnectionStyle` class provided.

### Canvas Control

**`NodiumGraphCanvas`** — Primary `TemplatedControl`. Hosts infinite world-coordinate canvas with nodes via DataTemplate, custom-rendered connections/grid/selection.

**Rendering order** (bottom to top): Grid → Connections → Drag preview → Node containers → Port visuals → Selection marquee → Validation feedback

### Built-in Interactions

- **Pan:** middle-mouse drag, Space+left-drag
- **Zoom:** scroll wheel (toward cursor), pinch, bindable `ViewportZoom`
- **Selection:** click (clear+select), Ctrl+click (toggle), marquee drag, Ctrl+marquee (additive)
- **Node drag:** left-drag, multi-drag on selection, optional snap-to-grid. Reports after drag completes, not during.
- **Connection draw:** drag from port → hover shows validation → release on target or cancel on empty space

## Non-Functional Requirements

- Smooth pan/zoom with 500+ nodes and 1000+ connections
- No reflection in hot paths (AOT-compatible)
- Testable via xUnit v3 + Avalonia headless

## Out of Scope

Undo/redo, layout algorithms, serialization, context menus, keyboard shortcuts, search/filter, connection validation logic, node content/appearance — all consumer responsibility.

## Claude special instructions

Always use maximum thinking effort. Take your time and think deeply about every problem.
Use jcodemunch-mcp for code lookup whenever available. Prefer symbol search, outlines, and targeted retrieval over reading full files. 

### Avalonia API usage

Always use the `mcp__avalonia-docs` MCP tools (`search_avalonia_docs`, `lookup_avalonia_api`, `get_avalonia_expert_rules`) to verify Avalonia API usage before writing Avalonia code. Do not rely on training data for Avalonia APIs — the project targets Avalonia 12 which has breaking changes from earlier versions. Key known differences:
- `IDataTemplate` lives in `Avalonia.Controls.Templates`, not `Avalonia.Controls`
- `ReadOnlyObservableCollection<T>.CollectionChanged` is an explicit interface implementation — cast to `INotifyCollectionChanged`
- `PointerWheelEventArgs` (not `PointerWheelChangedEventArgs` from Avalonia 11)
- `Space` is not a `KeyModifiers` flag — track it via `OnKeyDown`/`OnKeyUp`
- `Pen` constructor accepts `(IBrush, double, IDashStyle?)`