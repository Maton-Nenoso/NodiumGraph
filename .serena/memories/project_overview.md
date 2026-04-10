# NodiumGraph Project Overview

## Purpose
NodiumGraph is an open-source graph editor library for Avalonia (.NET). It provides interactive canvas primitives (pan, zoom, node dragging, selection, connection drawing) while leaving domain logic, styling, undo/redo, and serialization to the consumer.

## Tech Stack
- **Framework:** Avalonia 12 (.NET 10)
- **Language:** C# 12
- **Dependencies:** None beyond Avalonia (zero third-party deps)
- **Target:** AOT-compatible, single library in single namespace

## Architecture Principles
1. Base classes + strategy interfaces
2. Hybrid rendering (nodes as real Avalonia controls, connections/grid custom-rendered)
3. Report don't decide (handlers report interactions, consumer decides)
4. Small surface area (ship primitives only)

## Key Classes
- **Model:** Node, Port, Connection, Graph, IPortProvider, FixedPortProvider, DynamicPortProvider, CommentNode, GroupNode
- **Control:** NodiumGraphCanvas (primary TemplatedControl), CanvasOverlay, ConnectionRenderer, GridRenderer, MinimapRenderer
- **Interactions:** Handler interfaces (INodeInteractionHandler, IConnectionHandler, ISelectionHandler, ICanvasInteractionHandler)
- **Strategy:** IConnectionValidator, IConnectionRouter, IConnectionStyle
- **Utility:** Result/Result<T>, Error, ViewportTransform, NodeMoveInfo

## Non-Functional Requirements
- Smooth performance with 500+ nodes and 1000+ connections
- No reflection in hot paths (AOT)
- Testable via xUnit v3 + Avalonia headless
