# Strategy Interfaces Reference

Strategies let you swap algorithm implementations without subclassing the canvas. NodiumGraph exposes four: connection routing, connection validation, connection styling, and port provision. Each has a built-in default that `NodiumGraphCanvas` uses when the corresponding property is not set, so you can start with zero wiring and replace pieces as your application grows. The first three live in `NodiumGraph.Interactions`; port providers live in `NodiumGraph.Model` because they are part of a node's state rather than a canvas setting.

## IConnectionRouter

Namespace: `NodiumGraph.Interactions`

```csharp
public interface IConnectionRouter
{
    IReadOnlyList<Point> Route(Port source, Port target);
    RouteKind RouteKind { get; }
}
```

### Contract

- `Route(source, target)` returns an ordered list of world-space points that describes the path between the two ports.
- `RouteKind` tells the renderer how to interpret the list. For `RouteKind.Bezier`, the router must return exactly four points `[start, cp1, cp2, end]` — a cubic bezier segment. For `RouteKind.Polyline`, it returns two or more points interpreted as straight-line segments.
- The canvas calls `Route` on every render pass that affects a connection, so keep it cheap. The canvas also uses the axis-aligned bounding box of the returned points for viewport culling (cubic beziers stay inside their control hull, so the AABB is a conservative bound regardless of where the control points sit).
- Returning fewer than 2 points causes the connection to be skipped that frame.

### Built-in: BezierRouter

`NodiumGraph.Interactions.BezierRouter` — the canvas default. Returns a cubic bezier with horizontally-offset control points:

- Control offset = `max(|dx| * 0.4, 30.0)` where `dx` is the x-distance between endpoints.
- Control points are pushed in the direction of travel: left-to-right drags produce `cp1` to the right of `start` and `cp2` to the left of `end`; right-to-left drags mirror that.
- `RouteKind` is `RouteKind.Bezier`.

### RouteKind enum

```csharp
public enum RouteKind
{
    Polyline, // Straight-line segments.
    Bezier    // Cubic bezier curve (exactly 4 control points).
}
```

## IConnectionValidator

Namespace: `NodiumGraph.Interactions`

```csharp
public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
```

### Contract

- Called *during* a connection drag to provide live accept / reject feedback. The canvas invokes it on pointer move over a candidate target port, so the implementation must be fast.
- Return `true` if the connection is allowed, `false` to reject. On reject, the drag preview switches to the "invalid" visual style and `IConnectionHandler.OnConnectionRequested` is not called when the user releases — the drag ends as a no-op.
- Set `NodiumGraphCanvas.ConnectionValidator` to `null` to disable all built-in checks (not generally recommended — see below).

### Built-in: DefaultConnectionValidator

`NodiumGraph.Interactions.DefaultConnectionValidator`, accessed via `DefaultConnectionValidator.Instance` (singleton). This is the default the canvas uses unless you assign something else.

Rules, applied in order — the first that matches wins:

1. `ReferenceEquals(source, target)` → reject (self-loop on the same port).
2. `source.Owner == target.Owner` → reject (both ports live on the same node).
3. `source.Flow == target.Flow` → reject (output-to-output or input-to-input).
4. `Equals(source.DataType, target.DataType)` → accept; otherwise reject. Null `DataType` matches only null — null is not treated as a wildcard, so forgetting to set a `DataType` while another side has one will deterministically reject.

To layer custom rules on top of the defaults, delegate to the singleton:

```csharp
// Example — not from the repo.
file sealed class SingleIncomingValidator(Graph graph) : IConnectionValidator
{
    public bool CanConnect(Port source, Port target)
    {
        if (!DefaultConnectionValidator.Instance.CanConnect(source, target))
            return false;

        // Reject if the target already has an incoming connection.
        return !graph.Connections.Any(c => c.TargetPort == target);
    }
}
```

## IConnectionStyle

Namespace: `NodiumGraph.Interactions`

```csharp
public interface IConnectionStyle
{
    IBrush Stroke { get; }
    double Thickness { get; }
    IDashStyle? DashPattern { get; }
}
```

### Contract

Queried during render to obtain the pen for a connection. `NodiumGraphCanvas.DefaultConnectionStyle` is read once per frame; the canvas caches the resulting `Pen` by reference-comparing the three getter results, so you can mutate a style in place and changes pick up on the next render without allocating a new pen on every frame.

### Built-in: ConnectionStyle

`NodiumGraph.Interactions.ConnectionStyle`:

```csharp
public ConnectionStyle(
    IBrush? stroke = null,       // defaults to Brushes.Gray
    double thickness = 2.0,
    IDashStyle? dashPattern = null)
```

`thickness` must be positive — the constructor throws for zero or negative values.

## IPortProvider

Namespace: `NodiumGraph.Model`

```csharp
public interface IPortProvider
{
    IReadOnlyList<Port> Ports { get; }
    Port? ResolvePort(Point position, bool preview);
    void CancelResolve();
    event Action<Port>? PortAdded;
    event Action<Port>? PortRemoved;
}
```

### Contract

- `Ports` — the current set owned by this provider. The canvas enumerates it for rendering and hit-testing.
- `ResolvePort(position, preview)` — hit-tested in node-local coordinates. When `preview` is `true`, the result is tentative (for hover feedback) and must **not** create permanent state. When `preview` is `false`, the resolve is a commit and may create a new port (dynamic providers).
- `CancelResolve()` — rolls back the most recent uncommitted resolve. The canvas calls this when a drag is aborted so that dynamic providers can remove provisionally-created ports.
- `PortAdded` / `PortRemoved` — raised when `Ports` changes, so the canvas can invalidate its render cache.

### Built-in: FixedPortProvider

`NodiumGraph.Model.FixedPortProvider` — a static set of declared ports. Also implements [`ILayoutAwarePortProvider`](#ilayoutawareportprovider).

```csharp
public FixedPortProvider(double hitRadius = 20.0, bool layoutAware = false);
public FixedPortProvider(IEnumerable<Port> ports, double hitRadius = 20.0, bool layoutAware = false);
```

- `AddPort(Port port)` / `bool RemovePort(Port port)` mutate the port set and raise `PortAdded` / `PortRemoved`.
- `ResolvePort` returns the nearest port within the configured hit radius (constructor argument). `CancelResolve` is a no-op; the provider never creates tentative state.
- When `layoutAware: true`, `UpdateLayout` snaps every port to the nearest boundary point of the node's current `INodeShape`. This means you can declare ports at rough positions like `(0, 40)` and `(180, 40)` and they will settle onto the left and right edges of the node after measure.

### Built-in: DynamicPortProvider

`NodiumGraph.Model.DynamicPortProvider` — creates ports on demand at the nearest node boundary point.

```csharp
public DynamicPortProvider(
    Node owner,
    double reuseThreshold = 15.0,
    double maxDistance = 50.0);
```

- `ResolvePort` first projects `position` onto the owner's shape boundary. If an existing port is within `reuseThreshold` of that boundary point, it is returned. Otherwise, if `preview` is `false`, a new port is created at the boundary point and tracked as "last created". In `preview` mode, no new port is ever created.
- If the projected point is further than `maxDistance` from `position`, the resolve fails (`null`).
- `CancelResolve` removes the last port that `ResolvePort` created (if any), detaching it and raising `PortRemoved`. No-op if the last resolve reused an existing port.
- `AutoPruneOnDisconnect` (writable `bool`) — when `true`, calling `NotifyDisconnected(port, graph)` after removing a connection removes the port if it has no remaining edges. Consumer must drive this call; the canvas does not.
- `PruneUnconnected(graph)` removes every port in the provider that has no connection in `graph` — useful as a one-shot cleanup.

### ILayoutAwarePortProvider

Namespace: `NodiumGraph.Model`

```csharp
public interface ILayoutAwarePortProvider : IPortProvider
{
    void UpdateLayout(double width, double height, INodeShape? shape);
    event Action? LayoutInvalidated;
}
```

Providers that implement this interface are notified by the canvas after the owner node has been measured. `UpdateLayout` should recompute all port positions; `LayoutInvalidated` fires after a recompute so the canvas knows to redraw. `FixedPortProvider` (in layout-aware mode) uses this to snap ports to the shape boundary.

## See also

- [Handler interfaces reference](handlers.md)
- [Model reference](model.md)
- [Canvas control reference](canvas-control.md)
