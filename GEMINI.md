# GEMINI.md - NodiumGraph Project Context

## Project Overview
NodiumGraph is an open-source graph editor library for Avalonia. It provides an interactive canvas with pan, zoom, node dragging, selection, and connection drawing. The library is designed to be unopinionated about domain logic, styling, and undo/redo, leaving these to the consumer.

### Key Technologies
- **Framework:** Avalonia 12.0
- **Runtime:** .NET 10.0
- **Testing:** xUnit.v3, Avalonia Headless for UI testing
- **Language:** C# (Latest version)

### Architecture
- **NodiumGraphCanvas:** The primary `TemplatedControl`. It hosts node controls and an internal `CanvasOverlay`.
- **Hybrid Rendering:** Nodes are standard Avalonia controls driven by `DataTemplates`. Connections, the grid, and selection chrome are custom-rendered for performance.
- **Interfaces over Implementation:** Core logic is defined via interfaces (`INode`, `IPort`, `IConnection`, etc.), allowing consumers to plug in their own models.
- **Interaction Handlers:** User actions (moves, connections, deletions) are reported via handler interfaces (`INodeInteractionHandler`, `IConnectionHandler`, etc.) rather than the library mutating state directly.

## Building and Running

### Prerequisites
- .NET 10.0 SDK

### Key Commands
- **Build Solution:** `dotnet build`
- **Run Tests:** `dotnet test`
- **Run Sample App:** `dotnet run --project samples/NodiumGraph.Sample`

## Development Conventions

### Coding Style
- **Naming:** standard .NET PascalCase for public members, `_camelCase` for private fields.
- **Nullability:** Nullable reference types are enabled (`<Nullable>enable</Nullable>`).
- **Usings:** Implicit usings are enabled (`<ImplicitUsings>enable</ImplicitUsings>`).
- **Interfaces:** Prefer defining behavior through interfaces to maintain the "interfaces over opinions" design principle.

### Testing Practices
- **Unit Tests:** Located in `tests/NodiumGraph.Tests`.
- **UI Testing:** Uses `Avalonia.Headless.XUnit` for testing canvas interactions without a physical window.
- **Test Coverage:** Aim for high coverage on core model logic and interaction reporting.

### Project Structure
- `src/NodiumGraph/`: The main library project.
    - `Model/`: Core interfaces and default implementations for graph elements.
    - `Controls/`: UI controls, including `NodiumGraphCanvas` and specialized renderers.
    - `Interactions/`: Interface definitions for interaction handlers and routers.
    - `Themes/`: Default XAML styles and templates (e.g., `Generic.axaml`).
- `tests/NodiumGraph.Tests/`: Comprehensive test suite.
- `samples/NodiumGraph.Sample/`: A sample application demonstrating various node types and canvas features.

## Key Files
- `src/NodiumGraph/Controls/NodiumGraphCanvas.cs`: The "brain" of the library, handling input and orchestration.
- `docs/nodiumgraph-design.md`: The foundational design document outlining the library's philosophy and API.
- `Directory.Packages.props`: Centralized package version management.
- `src/NodiumGraph/Themes/Generic.axaml`: Default styles for the library's controls.
