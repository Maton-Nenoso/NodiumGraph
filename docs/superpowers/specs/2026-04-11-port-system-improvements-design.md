# Port System Improvements — Design Spec

**Date:** 2026-04-11
**Status:** Approved

## Overview

Six improvements to the NodiumGraph port system: simplify the Port model, consolidate providers, unify hit-testing, add connection capacity metadata, fix DynamicPortProvider cleanup, cache AbsolutePosition, and add observable collection changes.

This is a clean-break redesign (pre-1.0, no backwards compatibility constraints).

## Design Decisions

- **Breaking changes:** Allowed freely (option A)
- **Port capacity:** Model-level properties, consumer-enforced (option B) — Port exposes `MaxConnections`, library never refuses connections automatically
- **AnglePortProvider:** Removed — FixedPortProvider becomes layout-aware, DynamicPortProvider handles boundary placement
- **Auto-prune:** Cancelled drags always clean up; disconnected-port pruning is opt-in via `AutoPruneOnDisconnect` flag on DynamicPortProvider only

## 1. Port Model Simplification

### Removed from Port

- `Angle` — no longer needed without AnglePortProvider
- `LabelPlacement` — can be derived or moved to PortStyle

### Added to Port

- `MaxConnections` (int?, null = unlimited) — metadata for validators, not enforced by the library

### Kept on Port

- `Id`, `Owner`, `Name`, `Flow`, `Position`, `AbsolutePosition`, `Label`, `Style`

### AbsolutePosition Caching

`AbsolutePosition` is currently recomputed on every access (`new Point(Owner.X + Position.X, Owner.Y + Position.Y)`). With 500+ nodes and 4 ports each, this causes thousands of unnecessary allocations per frame.

**Fix:** Cache the computed value. Invalidate when:
- `Position` changes (port moved)
- `Owner.X` or `Owner.Y` changes (node moved)

Port subscribes to `Owner.PropertyChanged` and invalidates the cache on `X` or `Y` changes.

## 2. Provider Consolidation

### Removed

- `AnglePortProvider` — replaced by layout-aware FixedPortProvider
- `INodeShape.GetBoundaryPoint(double angleDegrees, double width, double height)` — replaced by nearest-boundary-point method

### INodeShape (refocused)

```csharp
public interface INodeShape
{
    Point GetNearestBoundaryPoint(Point position, double width, double height);
}
```

Implementations (`RectangleShape`, `EllipseShape`, `RoundedRectangleShape`) updated to implement the new method. Used by DynamicPortProvider for boundary snapping and optionally by layout-aware FixedPortProvider for repositioning on resize.

### IPortProvider Interface

```csharp
public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point worldPosition, bool preview);
    void CancelResolve();
    event Action<Port>? PortAdded;
    event Action<Port>? PortRemoved;
}
```

- `ResolvePort(Point, bool preview)` — when `preview: true`, returns nearest port for visual feedback without creating. When `preview: false`, may create (DynamicPortProvider).
- `CancelResolve()` — provider cleans up the last port it created during resolve. DynamicPortProvider removes it; FixedPortProvider is a no-op.
- `PortAdded` / `PortRemoved` — canvas subscribes for re-render triggers.

### ILayoutAwarePortProvider (unchanged)

```csharp
public interface ILayoutAwarePortProvider : IPortProvider
{
    void UpdateLayout(double width, double height, INodeShape? shape);
    event Action? LayoutInvalidated;
}
```

### FixedPortProvider

- Declared ports at known positions
- Configurable hit radius (constructor parameter, default 20.0)
- `AddPort(Port)` / `RemovePort(Port)` for mutability after construction
- Fires `PortAdded` / `PortRemoved` events
- Optionally implements `ILayoutAwarePortProvider` to reposition ports on node resize using `INodeShape.GetNearestBoundaryPoint`
- `CancelResolve()` is a no-op

### DynamicPortProvider

- Creates ports on-demand at boundary intersection
- Uses `INodeShape.GetNearestBoundaryPoint` instead of hardcoded rectangle math
- Reuse threshold (default 15.0) and max distance (default 50.0) remain configurable
- `CancelResolve()` removes the last dynamically created port
- `AutoPruneOnDisconnect` (bool, default `false`) — when enabled, removes ports with zero connections after a disconnection event. Only applies to ports created by this provider.
- `PruneUnconnected(Graph graph)` — manual cleanup, always available
- Fires `PortAdded` / `PortRemoved` events

## 3. Hit-Test Unification

The canvas currently has two separate hit-test paths:

1. `HitTestPort()` — hardcoded 20.0 radius, visual preview only
2. `ResolvePortForConnection()` — delegates to provider's `ResolvePort`

These can disagree silently (port highlights on hover but doesn't resolve on release, or vice versa).

**Fix:** Remove the canvas-side hardcoded radius. Both preview and commit go through `provider.ResolvePort(worldPos, preview)`. The provider is the single source of truth for hit radius.

- Preview (`OnPointerMoved`): `ResolvePort(worldPos, preview: true)`
- Commit (`OnPointerReleased`): `ResolvePort(worldPos, preview: false)`

No heap allocations in the hot path — `Point` is a value type, distance math is scalar.

## 4. DynamicPortProvider Cleanup

### Cancelled Drag (always)

1. During drag, canvas tracks `_lastResolvedProvider` (whichever provider returned a port)
2. On cancel (mouse released on empty space), canvas calls `_lastResolvedProvider.CancelResolve()`
3. DynamicPortProvider removes the last port it created; FixedPortProvider does nothing
4. Canvas resets `_lastResolvedProvider` to null

### Disconnected Port (opt-in)

When `AutoPruneOnDisconnect` is `true`:
- Canvas subscribes to `Graph.Connections.CollectionChanged`
- On connection removal, canvas identifies affected ports and notifies their providers
- DynamicPortProvider checks if the port has zero remaining connections and removes it
- FixedPortProvider ignores the notification (its ports are intentionally declared)

### Manual Cleanup (always available)

`PruneUnconnected(Graph graph)` — iterates provider's ports, checks if any connection in the graph references them, removes those with zero connections. Consumer calls this when needed.

## 5. Observable Port Collections

`PortAdded` and `PortRemoved` events on `IPortProvider` allow the canvas to subscribe and invalidate rendering when ports change.

- `FixedPortProvider.AddPort(Port)` fires `PortAdded`
- `FixedPortProvider.RemovePort(Port)` fires `PortRemoved`
- `DynamicPortProvider.ResolvePort(pos, false)` fires `PortAdded` when creating
- `DynamicPortProvider.CancelResolve()` fires `PortRemoved`
- `DynamicPortProvider.PruneUnconnected()` fires `PortRemoved` per port
- Auto-prune-on-disconnect fires `PortRemoved`

## 6. Canvas Wiring Changes

- Remove `HitTestPort()` method with hardcoded radius
- `ResolvePortForConnection()` renamed or refactored to use `ResolvePort(pos, preview)` on each provider
- Subscribe to `PortAdded` / `PortRemoved` on each node's provider for render invalidation
- Track `_lastResolvedProvider` for cancel cleanup
- Subscribe to `Graph.Connections.CollectionChanged` to notify providers of disconnections (for auto-prune)

## Files Affected

| File | Change |
|------|--------|
| `Model/Port.cs` | Remove Angle, LabelPlacement. Add MaxConnections. Cache AbsolutePosition. |
| `Model/IPortProvider.cs` | Add preview param to ResolvePort, add CancelResolve, add events |
| `Model/FixedPortProvider.cs` | Add AddPort/RemovePort, fire events, optionally implement ILayoutAwarePortProvider |
| `Model/DynamicPortProvider.cs` | Add CancelResolve, AutoPruneOnDisconnect, PruneUnconnected, use INodeShape, fire events |
| `Model/AnglePortProvider.cs` | **Delete** |
| `Model/INodeShape.cs` | Replace GetBoundaryPoint with GetNearestBoundaryPoint |
| `Model/RectangleShape.cs` | Implement GetNearestBoundaryPoint |
| `Model/EllipseShape.cs` | Implement GetNearestBoundaryPoint |
| `Model/RoundedRectangleShape.cs` | Implement GetNearestBoundaryPoint |
| `Model/ILayoutAwarePortProvider.cs` | No change |
| `Model/PortLabelPlacement.cs` | **Delete** (or move to PortStyle) |
| `Controls/NodiumGraphCanvas.cs` | Unify hit-test, track _lastResolvedProvider, subscribe to port/connection events |
| `Controls/CanvasOverlay.cs` | Update port rendering (no more Angle-based auto-placement) |
| Sample app (`MainWindow.axaml.cs`) | Update to new API (remove AnglePortProvider usage) |
