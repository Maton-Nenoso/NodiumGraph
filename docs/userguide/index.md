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
- [Bind ViewportZoom, ViewportOffset, and selection](how-to/bind-viewport.md)
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
