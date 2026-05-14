# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

NodiumGraph is an open-source graph editor library for **Avalonia** (.NET). It provides interactive canvas primitives — pan, zoom, node dragging, selection, and connection drawing — while leaving domain logic, styling, undo/redo, and serialization to the consumer.

- **Owner:** Maton-Nenoso (Stefan Maton)
- **License:** MIT
- **Target:** Avalonia 12, .NET 10, AOT-compatible, zero third-party deps beyond Avalonia

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

## Codex special instructions

Always use maximum thinking effort. Take your time and think deeply about every problem.

### Avalonia API usage

Always use the `mcp__avalonia-docs` MCP tools (`search_avalonia_docs`, `lookup_avalonia_api`, `get_avalonia_expert_rules`) to verify Avalonia API usage before writing Avalonia code. Do not rely on training data for Avalonia APIs — the project targets Avalonia 12 which has breaking changes from earlier versions. Key known differences:
- `IDataTemplate` lives in `Avalonia.Controls.Templates`, not `Avalonia.Controls`
- `ReadOnlyObservableCollection<T>.CollectionChanged` is an explicit interface implementation — cast to `INotifyCollectionChanged`
- `PointerWheelEventArgs` (not `PointerWheelChangedEventArgs` from Avalonia 11)
- `Space` is not a `KeyModifiers` flag — track it via `OnKeyDown`/`OnKeyUp`
- `Pen` constructor accepts `(IBrush, double, IDashStyle?)`

<claude-mem-context>
# Memory Context

# [NodiumGraph] recent context, 2026-05-14 1:43pm GMT+2

Legend: 🎯session 🔴bugfix 🟣feature 🔄refactor ✅change 🔵discovery ⚖️decision 🚨security_alert 🔐security_note
Format: ID TIME TYPE TITLE
Fetch details: get_observations([IDs]) | Search: mem-search skill

Stats: 50 obs (23,100t read) | 450,200t work | 95% savings

### May 14, 2026
1399 12:22p 🔵 AXAML files exist but contain no port declarations
1400 " 🔵 Node ports are not defined in AXAML DataTemplates; only visual appearance
S415 Design AXAML-based port definition capability for NodiumGraph nodes; initial investigation and architectural sketch (May 14, 12:22 PM)
1401 12:23p 🔵 Port and FixedPortProvider architecture prevents XAML declarability
1402 " 🔵 PortAnchor.cs file exists; additional port-related infrastructure identified
1403 12:24p 🔵 PortAnchor-based architecture enables declarative port positioning on node edges
S416 Design decisions for PortDefinition and AXAML node port declaration in a node-based UI library (May 14, 12:24 PM)
S417 Design and formalize specification for declarative AXAML port definitions in NodiumGraph (May 14, 12:29 PM)
1404 12:32p 🟣 Declarative AXAML port definitions specification
S418 User asked whether ports can be defined on nodes from within AXAML file definitions; Claude performed triage of code review comments against the port declaration feature specification. (May 14, 12:32 PM)
S419 Architectural decision: how should port definitions declared in AXAML become accessible to code? User asserted that "the presenter is per node type definition" and "ports shouldn't be defined in AXAML if they aren't usable afterwards." Claude analyzed three viable approaches and recommended one. (May 14, 12:36 PM)
S420 Redesign NodiumGraph AXAML port declaration architecture: move from rendering-side-effect model (ports materialized in NodePresenter.OnAttachedToVisualTree) to declarative per-type model with registry-backed lazy materialization (May 14, 12:42 PM)
1405 12:48p ⚖️ Node graph topology decoupled from rendering via declarative NodeTemplate registry
1406 12:50p ⚖️ Declarative AXAML port definitions via NodeTemplate registry (revised architecture)
S421 Design review and refinement for declarative AXAML port definitions — answer whether ports can be defined inline in AXAML node templates, and finalize the specification with complete design, testing strategy, and implementation contract. (May 14, 12:51 PM)
1407 12:55p ⚖️ NodePortRegistry type-hierarchy lookup and thread-safety specifications
1408 12:56p ⚖️ Lazy materialization on both Node.Ports and Node.PortProvider getters
1409 " 🔵 Registry lookup retry semantics and late registration behavior clarified
1410 12:57p ✅ Design decision matrix finalized with backward compatibility and concurrency details
1411 " ✅ Comprehensive test specification for registry type-hierarchy lookup, late registration, and materialization
1412 " ✅ Registration timing table clarified for access-time semantics and retry behavior
1413 12:58p ✅ Removed obsolete non-goal: Pre-InitializeComponent access
1414 " ✅ Removed type-hierarchy lookup from future/out-of-scope — now core feature
1415 " ✅ Public surface delta clarified: PortProvider getter behavior change documented
1416 " ✅ Declarative AXAML ports design specification committed
S422 Design review findings addressed: five critical issues (P1×2, P2×2, P3×1) in declarative Axaml ports plan resolved through design clarifications and concrete implementation specifications (May 14, 12:58 PM)
1417 1:18p 🔵 Critical design issues identified in declarative Axaml ports plan
1418 1:19p 🔵 Confirmed double-subscription vulnerability in AddNodeContainer canvas code
1419 1:20p ✅ Design document clarified to address P1 and P3 review findings
1420 " ✅ Design document specified immutable snapshot implementation for registry
1421 " ✅ NodePortRegistry.TryGet signature updated to return PortSpec snapshots
1422 1:21p ✅ Replaced hand-wavy thread safety with concrete locking algorithm
1423 " ✅ Implemented _portProviderExplicit sentinel to enforce null assignment semantics
1424 " ✅ Specified required NodiumGraphCanvas changes to fix double-subscription in AddNodeContainer
1425 1:22p ✅ Added explicit immutability and concurrency tests to NodePortRegistryTests specification
1426 " ✅ Added explicit null suppression test to NodeRegistryMaterializationTests
1427 " ✅ Added canvas double-attach prevention test to DeclarativeNodeTemplateTests
1428 " ✅ Expanded Public surface delta to document PortSpec and canvas changes
S423 Design review response: address P1 (PortSpec visibility), P2 (empty-Ports behavior and template matching semantics), and P3 (shallow-snapshot immutability) findings in declarative AXAML ports specification. (May 14, 1:23 PM)
1429 1:28p 🔵 Design review findings on declarative AXAML ports specification
1430 1:29p ✅ Design specification updated to address template matching and empty-ports issues
1431 1:30p ✅ Design documentation clarified for exact-type matching and empty-ports handling
1432 " ⚖️ Registry lookup changed from type-hierarchy walk-up to exact-type matching only
1433 " ✅ DataType validation constraint added to enforce immutability safety
1434 " ✅ PortSpec promoted to public API with immutability clarification
1435 " ✅ Open question resolutions table updated with final design decisions
1436 " ✅ Test specifications updated for exact-type-only registry lookup
1437 1:31p ✅ Integration tests added for edge cases: visual-only templates, exact-type matching, and DataType validation
1438 " ✅ Conflict policy documentation simplified by DataType validation constraint
1439 " ✅ Open questions table updated: conflict policy row simplified by DataType validation
1440 " ✅ Out-of-scope documentation added for type-hierarchy lookup walk-up
1441 1:32p ✅ Declarative AXAML ports design specification finalized and committed
1442 1:38p 🔵 Design contradictions identified in declarative AXAML ports registry document
1443 " ✅ Fixed exact-type matching contradiction in NodePortRegistry.TryGet
1444 " ✅ Specified storage immutability guarantees for registry snapshots
1445 " ✅ Tightened test isolation contract for registry state observability
1446 " ✅ Documented hot-reload conflict policy and Clear() requirement
1447 " ✅ Added explicit test case for non-downcastable snapshot returns
1448 1:39p ✅ Finalized declarative XAML ports design with all review issues resolved
S424 Review and resolve design contradictions in declarative XAML ports registry specification; address exact-type matching logic, immutability guarantees, hot-reload behavior, and test isolation assumptions. (May 14, 1:39 PM)
**Investigated**: Examined declarative AXAML ports design document (docs/plans/2026-05-14-declarative-axaml-ports-design.md) and identified four design contradictions: TryGet method claiming exact-only but algorithm walking base types; hot-reload section not documenting NodePortRegistry.Clear() requirement; immutable snapshot claim contradicted by IReadOnlyList return type allowing downcasting; test isolation assumptions ignoring parallelism race conditions on shared static registry state.

**Learned**: Design specification had internal inconsistencies between prose documentation and concrete algorithm descriptions. IReadOnlyList interface alone provides insufficient protection against downcasting attacks if underlying implementation uses mutable collections. Test isolation on shared static state requires explicit contract that includes all observing tests, not just mutating tests. Exact-type matching must be enforced consistently across both NodeTemplate.Match and NodePortRegistry.TryGet to prevent visual/port topology drift based on template declaration order under Avalonia's order-dependent selection.

**Completed**: Fixed all four design issues in specification: (1) Removed walk-up loop from TryGet algorithm, now single exact-type key lookup; (2) Explicitly specified storage as ReadOnlyCollection<PortSpec> or ImmutableArray<PortSpec> over private backing array to prevent downcasting; (3) Updated hot-reload registration timing table to require explicit NodePortRegistry.Clear() call before re-parsing changed templates; (4) Broadened test isolation contract to include all registry-observing tests in non-parallel xUnit collection; (5) Added new test case "Snapshot non-downcastable" with explicit assertion guidance. Changes committed to main branch as 534d62c with comprehensive commit message summarizing all fixes.

**Next Steps**: Primary session is at decision point: either conduct another design review pass to catch remaining gaps, or transition to implementation planning phase (writing-plans) to begin detailed task breakdown for coding the registry, Node materialization, NodeTemplate, and canvas integration changes.


Access 450k tokens of past work via get_observations([IDs]) or mem-search skill.
</claude-mem-context>