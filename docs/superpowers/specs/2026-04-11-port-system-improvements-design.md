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

- `MaxConnections` (uint?, null = unlimited) — metadata for validators, not enforced by the library. Uses `uint?` to prevent nonsensical negative values.

### Kept on Port

- `Id`, `Owner`, `Name`, `Flow`, `Position`, `AbsolutePosition`, `Label`, `Style`

### AbsolutePosition Caching

`AbsolutePosition` is currently recomputed on every access (`new Point(Owner.X + Position.X, Owner.Y + Position.Y)`). While `Point` is a value type (no heap allocation), the repeated arithmetic and property reads across 500+ nodes with 4 ports each add unnecessary CPU work per frame.

**Fix:** Cache the computed value. Invalidate when:
- `Position` changes (port moved)
- `Owner.X` or `Owner.Y` changes (node moved)

Port subscribes to `Owner.PropertyChanged` and invalidates the cache on `X` or `Y` changes. The subscription is established in the constructor. When a port is removed from a provider (via `RemovePort`, `CancelResolve`, or pruning), the provider calls a `Detach()` method on Port that unsubscribes from Owner's PropertyChanged to prevent leaking removed ports.

## 2. Provider Consolidation

### Removed

- `AnglePortProvider` — replaced by layout-aware FixedPortProvider
- `INodeShape.GetBoundaryPoint(double angleDegrees, double width, double height)` — replaced by nearest-boundary-point method

### INodeShape (refocused)

```csharp
public interface INodeShape
{
    /// <param name="position">Center-relative coordinates (0,0 = node center)</param>
    /// <returns>Center-relative point on the boundary</returns>
    Point GetNearestBoundaryPoint(Point position, double width, double height);
}
```

All coordinates are **center-relative** (consistent with the existing `GetBoundaryPoint` convention). Callers convert to/from world-space or top-left-relative as needed.

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

- `ResolvePort(Point, bool preview)` — when `preview: true`, returns nearest existing port for visual feedback without side effects (no port creation, no state changes). When `preview: false`, may create a new port (DynamicPortProvider). FixedPortProvider ignores the flag since it never creates.
- `CancelResolve()` — provider cleans up the last port it created via `ResolvePort(pos, false)`. DynamicPortProvider removes it; FixedPortProvider is a no-op. Only the single most recently created port is tracked — when `ResolvePort(pos, false)` is called again, the previous "last created" is replaced (the earlier port persists as a committed port).
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
- Implements `ILayoutAwarePortProvider` with a constructor flag `layoutAware` (default `false`). When `true`, `UpdateLayout` repositions ports to the nearest boundary point using `INodeShape.GetNearestBoundaryPoint`. When `false`, `UpdateLayout` is a no-op.
- `CancelResolve()` is a no-op

### DynamicPortProvider

- Creates ports on-demand at boundary intersection
- Uses `INodeShape.GetNearestBoundaryPoint` instead of hardcoded rectangle math
- Reuse threshold (default 15.0) and max distance (default 50.0) remain configurable
- `CancelResolve()` removes the last dynamically created port
- `AutoPruneOnDisconnect` (bool, default `false`) — when enabled, removes ports with zero connections after a disconnection event. All ports in a DynamicPortProvider are dynamically created, so all are candidates for pruning.
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

### Unsuccessful Commit (always)

The canvas calls `CancelResolve()` on the last resolved provider whenever a connection is **not** successfully established. This includes:

1. **Empty-space cancel** — user releases on empty space (no target port found)
2. **Validation rejection** — `CanConnect(source, target)` returns false
3. **Handler failure** — `OnConnectionRequested` returns a failed `Result<Connection>`

Flow:
1. On commit: canvas calls `ResolvePort(pos, false)` on each node's provider to get the target port, and records which provider returned it as `_commitProvider`
2. Canvas runs validation (`CanConnect`) and handler (`OnConnectionRequested`)
3. If validation fails or handler rejects, canvas calls `_commitProvider.CancelResolve()` to roll back the port created during this commit
4. Only on successful connection establishment does the port persist
5. Canvas resets `_commitProvider` to null

Note: `_commitProvider` is tracked separately from preview-time state. The preview path (`ResolvePort(pos, true)`) never creates ports and has no rollback concern. The commit path may hit a different provider than preview did (e.g., preview returned null but commit creates a dynamic port), so rollback must always target the commit-time provider.

### Disconnected Port (opt-in)

When `AutoPruneOnDisconnect` is `true`:
- DynamicPortProvider exposes a `NotifyDisconnected(Port port, Graph graph)` method
- Canvas subscribes to `Graph.Connections.CollectionChanged`; on removal, calls `NotifyDisconnected` on the port's owner's provider (if it is a DynamicPortProvider)
- DynamicPortProvider checks if the port has zero remaining connections in the graph and removes it
- This method is specific to DynamicPortProvider, not on IPortProvider — FixedPortProvider ports are never auto-pruned

### Manual Cleanup (always available)

`PruneUnconnected(Graph graph)` — iterates provider's ports, checks if any connection in the graph references them, removes those with zero connections. Consumer calls this when needed.

## 5. Observable Port Collections

`PortAdded` and `PortRemoved` events on `IPortProvider` allow the canvas to subscribe and invalidate rendering when port membership changes.

- `FixedPortProvider.AddPort(Port)` fires `PortAdded`
- `FixedPortProvider.RemovePort(Port)` fires `PortRemoved`
- `DynamicPortProvider.ResolvePort(pos, false)` fires `PortAdded` when creating
- `DynamicPortProvider.CancelResolve()` fires `PortRemoved`
- `DynamicPortProvider.PruneUnconnected()` fires `PortRemoved` per port
- Auto-prune-on-disconnect fires `PortRemoved`

### Port Property Changes

Collection events cover membership but not mutations to existing ports (Position, Label, Style changes, layout-driven repositioning). The canvas also needs to redraw when a port's visual properties change.

Port already implements `INotifyPropertyChanged`. The canvas subscribes to `PropertyChanged` on each port (via `PortAdded`, unsubscribes via `PortRemoved`) and invalidates the visual layer on relevant property changes (`Position`, `Label`, `Style`). This also covers layout-driven repositioning where the provider updates `Port.Position` after a resize.

### PortStyle Mutation

`PortStyle` is mutable and implements `INotifyPropertyChanged`. If a consumer updates `port.Style.Fill` without replacing the `Style` instance, `Port.PropertyChanged` for `Style` does not fire. To handle this:

- When a port's `Style` is set (including initial assignment), the canvas subscribes to `PortStyle.PropertyChanged` on the style instance
- When `Style` is replaced, the canvas unsubscribes from the old instance and subscribes to the new one
- Any `PortStyle.PropertyChanged` event invalidates the port visual layer

## 6. Canvas Wiring Changes

- Remove `HitTestPort()` method with hardcoded radius
- `ResolvePortForConnection()` renamed or refactored to use `ResolvePort(pos, preview)` on each provider
- Subscribe to `PortAdded` / `PortRemoved` on each node's provider for render invalidation
- Subscribe to `Port.PropertyChanged` (via PortAdded/PortRemoved lifecycle) for port mutation redraw
- Track `_lastResolvedProvider` for cancel/rollback cleanup
- Call `CancelResolve()` on any unsuccessful commit (validation fail, handler rejection), not just empty-space cancel
- Subscribe to `Graph.Connections.CollectionChanged` to notify providers of disconnections (for auto-prune)
- Handle `Node.PortProvider` replacement: `Node` fires `PropertyChanged` for `PortProvider`. Canvas subscribes to `Node.PropertyChanged`; on `PortProvider` change, unsubscribes from old provider's events (PortAdded/PortRemoved and all per-port PropertyChanged subscriptions), subscribes to new provider's events, and re-subscribes to existing ports in the new provider.

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
| `Model/Node.cs` | Ensure `PortProvider` setter fires `PropertyChanged` for canvas resubscription |
| `Model/ILayoutAwarePortProvider.cs` | No change |
| `Model/PortLabelPlacement.cs` | **Delete** — label placement moves to `PortStyle.LabelPlacement` (enum stays, file moves) |
| `Controls/NodiumGraphCanvas.cs` | Unify hit-test, track _lastResolvedProvider, subscribe to port/connection events |
| `Controls/CanvasOverlay.cs` | Update port rendering — label placement reads from `PortStyle.LabelPlacement` with a position-based default heuristic (left/right of node center) replacing the angle-based auto-placement |
| Sample app (`MainWindow.axaml.cs`) | Update to new API (remove AnglePortProvider usage) |
