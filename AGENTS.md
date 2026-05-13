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

# [NodiumGraph] recent context, 2026-05-13 5:52pm GMT+2

Legend: 🎯session 🔴bugfix 🟣feature 🔄refactor ✅change 🔵discovery ⚖️decision 🚨security_alert 🔐security_note
Format: ID TIME TYPE TITLE
Fetch details: get_observations([IDs]) | Search: mem-search skill

Stats: 50 obs (20,294t read) | 385,082t work | 95% savings

### May 13, 2026
1252 5:21p ✅ Clarified breaking changes summary to list both deleted Port constructors
1253 " ✅ Expanded test strategy with canvas invalidation chain coverage and user guide migration tasks
1254 " ✅ Finalized done criteria with concrete test specifications and documentation validation
1256 5:25p 🔵 Code review findings: Canvas invalidation, arc-length formula, and test strategy gaps
1257 5:26p 🔵 P1 validation: InvalidateConnectionGeometryForNode exists but uncalled on Port.AbsolutePosition change
1258 " ✅ P2 fix: Corrected RoundedRectangleShape arc-length formula in design doc
1259 " ✅ P1 fix: Clarified canvas invalidation chain and explicit connection-geometry cache invalidation
1260 " ✅ P3 fix: Updated RoundedRectangleShapeTests strategy from corner-snap to corner-arc round-trip contract
1261 5:27p ✅ Minor fix: Clarified that rounded-rectangle corner-arc ports are reachable via both dynamic and fixed anchors
1262 " ✅ Design document rev 3 committed with all code-review corrections
1263 5:29p ⚖️ Added canonical anchor rule for shared boundary endpoints
1264 " ✅ Added EmissionDirection re-fire to Port invalidation chain for Width/Height/Shape changes
1265 " ✅ Clarified PortTests strategy for EmissionDirection INPC firing
1266 5:30p ✅ Design document rev 4 committed with canonical anchor tie-break and EmissionDirection INPC
1267 " ✅ Design document updated with canonical anchor and emission direction property-change fixes
1268 5:31p ✅ Expanded round-trip property test strategy with three explicit canonicalization cases
1269 " ✅ Expanded done criteria #2 with explicit canonicalization test cases
1270 " ✅ Design document rev 5 committed with round-trip test wording alignment
S401 Review design document for anchor-based port positioning system in NodiumGraph (May 13, 5:31 PM)
S402 Design review refinements for anchor-based port positioning — address P1/P2/P3 issues: enum validation, null policy, zero-dimension fallbacks, ellipse orientation mapping, rounded-rectangle parameterization (May 13, 5:34 PM)
1271 5:35p ⚖️ Added explicit PortEdge enum validation to PortAnchor constructor
1272 5:36p ⚖️ Specified ellipse edge fraction orientation and rounded-rectangle radius clamping in design
1273 " ⚖️ Added null validation and zero-dimension fallback contract to Node.Shape and shape methods
1274 " ✅ Expanded test strategy with explicit enum validation, null-rejection, and zero-dimension test cases
1275 " ✅ Refined shape test criteria with endpoint validation and capsule-case coverage
1276 5:37p ✅ Committed anchor-based port positioning design plan (revision 6) with all P1/P2 refinements
S403 Review of anchor-based port positioning design document (2026-05-13) (May 13, 5:37 PM)
S404 Memory agent checkpoint: design plan revision 7 committed with per-edge capsule specification, PortAnchor in shape API, and Width/Height property clarification (May 13, 5:39 PM)
1277 5:39p ⚖️ INodeShape interface refactored to accept PortAnchor instead of raw (edge, fraction) pairs
1278 " ✅ Node class forwarding methods updated to accept PortAnchor instead of raw (edge, fraction)
1279 " 🔵 Found three locations in design doc still decomposing PortAnchor into (Edge, Fraction) for shape calls
1280 " ✅ Fixed code examples in design document: replaced decomposed (Anchor.Edge, Anchor.Fraction) with validated PortAnchor
1281 5:40p ✅ Completed API consistency fixes: all decomposed (Anchor.Edge, Anchor.Fraction) calls now use PortAnchor directly
1282 " ✅ Refined RoundedRectangleShape specification with per-edge dimension table and degenerate case clarity
1283 " ✅ Clarified Node dimension mutation scope: Width/Height are internal-only, Shape is public consumer action
S405 Review anchor-based port positioning design document and conduct codebase impact analysis to identify all sites requiring migration (May 13, 5:41 PM)
1284 5:44p ⚖️ Anchor-based port positioning architecture for NodiumGraph
1285 " 🔵 User guide documentation omitted from migration list
1286 " 🔵 Test migration scope underestimates affected test suites
1287 " ✅ Zero-dimension table documentation uses outdated API method signatures
1288 " ⚖️ Architecture design approved for implementation
S406 Review and refine anchor-based port positioning migration plan based on scope analysis findings (May 13, 5:45 PM)
1289 5:45p 🔵 22 test files use deleted Point-based Port constructor
1290 " 🔵 User guide model.md still documents Point-based Port constructors
1291 " ✅ Design plan updated with correct PortAnchor method signatures
1292 " ✅ Test strategy expanded to cover all 22 affected test suites
1293 " ✅ Done criteria clarified to include comprehensive userguide sweep
1294 " ⚖️ Migration plan finalized with broadened scope and test strategy
S407 Verify and refine anchor-based port positioning design document; address identified gaps from impact analysis and confirm implementation readiness (May 13, 5:45 PM)
1295 5:47p ✅ Anchor-based port positioning design document refined with expanded test strategy and doc scope
S408 Analyze and refine anchor-based port positioning migration plan; complete specification design and prepare for implementation phase (May 13, 5:47 PM)
1296 5:48p ✅ RectangleShape boundary parameterization fully specified
1297 " ✅ Plan updated with rectangle parameterization specification and committed
S409 Comprehensive review of design document completeness and identification of documentation scope gaps for anchor-based port positioning implementation (May 13, 5:48 PM)
1298 5:49p ✅ RectangleShape boundary parameterization table added to design specification
1300 5:50p 🔵 custom-router.md documents AbsolutePosition invalidation partially
1301 " ✅ Plan expanded to include additional userguide pages discovered during documentation audit
1302 " ✅ Done criterion 6 specified with concrete failure conditions and comprehensive sweep scope
1303 " ✅ Plan finalized with comprehensive documentation migration scope and testable done criteria
S410 Complete anchor-based port positioning migration plan specification through comprehensive scope discovery and documentation audit (May 13, 5:50 PM)
**Investigated**: • Initial findings from code review: P2 gaps in model.md and test migration scope; P3 zero-dimension wording inconsistency
    • Grep analysis of documentation: found 22 test files using deleted Point-based Port constructors; confirmed model.md lines 100–101 document deleted API
    • Additional documentation audit: identified persist-graph-state.md, custom-router.md, and rendering-pipeline.md as affected pages with specific guidance needs
    • Plan specification: all three shape boundary parameterizations (Rectangle, Ellipse, RoundedRectangle) with complete formulas and round-trip contracts

**Learned**: • Migration scope encompasses 22 test files across model, rendering, routing, and canvas domains — much broader than initially-named rows; test helper (TestNodes.WithPorts) can reduce churn
    • Documentation gaps extend beyond Port construction examples to persistence guidance (should serialize anchors, not derived Position), routing documentation (incomplete invalidation contract), and rendering notes (Position derivation)
    • Specification rigor: all three shapes require symmetric per-edge endpoint tables, canonical ownership rules, and clockwise parameterization consistency; Bottom/Left edges use (1-Fraction) formulas
    • Done criteria must be swept and specific: enumerating four failure conditions (old API references, Point examples, world-unit persistence, node-move-only invalidation) is more testable than generic "no stale references"

**Completed**: • Commit 759d253 (rev 8): Broadened userguide to include 3-reference/model.md; added explicit 22-file test row with TestNodes helper; updated done criteria #6 to comprehensive sweep; fixed zero-dim signatures to (anchor, w, h)
    • Commit 0577114 (rev 9): Added explicit RectangleShape boundary parameterization table with formulas and endpoints for all four edges
    • Commit fa6c7ad (rev 10): Expanded userguide targets to 10 pages (added persist-graph-state.md, custom-router.md, rendering-pipeline.md with specific guidance); rewrote done criteria #6 as enumerated sweep conditions
    • Plan specification now complete through revision 10 with symmetric shape specs, comprehensive test strategy (22 files + new tests), explicit documentation targets with guidance, and testable done criteria

**Next Steps**: Plan specification is complete and locked at revision 10. The user indicated they are ready to proceed. Next phase is converting comprehensive design spec into granular implementation tasks via writing-plans skill. This will break the plan into specific tasks for: PortAnchor value type, INodeShape method implementations per shape, Port position caching/invalidation, provider updates, router integration, canvas invalidation chain, 22-file test migration, documentation updates to 10 pages, and sample app updates.


Access 385k tokens of past work via get_observations([IDs]) or mem-search skill.
</claude-mem-context>