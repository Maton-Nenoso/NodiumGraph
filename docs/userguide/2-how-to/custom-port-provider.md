# Write a Custom IPortProvider

## Goal

Control how ports appear on a node — whether they are fixed, created dynamically at the edge the user drags from, driven by a schema, or something else entirely.

## Prerequisites

- You already host `NodiumGraphCanvas` and have a `Node` subclass. See [Host the Canvas](host-canvas.md) and [Subclass the model](subclass-model.md).
- You've read the port-provider section of the [strategies reference](../3-reference/strategies.md#iportprovider). The built-ins (`FixedPortProvider`, `DynamicPortProvider`) cover most real-world cases — write your own only when they don't.

## Steps

### 1. When you need a custom provider

First, check that neither built-in fits. Most NodiumGraph apps never touch this interface.

- **`FixedPortProvider`** — fits any node with a known, declared port set. Add ports with `AddPort(Port)`, remove with `RemovePort(Port)`. Declare ports with a `PortAnchor` (edge + fraction) and the library derives the world-unit position automatically after each measure.
- **`DynamicPortProvider`** — fits nodes where the user should be able to create ports by dragging a connection from an arbitrary point on the boundary. Configure `reuseThreshold` and `maxDistance` to taste; optionally enable `AutoPruneOnDisconnect` to delete unused ports automatically.
- **Custom provider** — only needed when port identity is governed by domain rules the built-ins don't know about: a schema-driven "record" node with one port per field, a provider that delegates to another subsystem, a provider that hydrates ports lazily from a database.

### 2. The contract

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

- **`Ports`** — the current set of ports the canvas should render and hit-test. The canvas enumerates this on every layout invalidation.
- **`ResolvePort(position, preview)`** — asked "is there a port near this node-local point?" on pointer hover during a connection drag. Returns a `Port` if yes, `null` if no. **`preview: true`** means the call is tentative (hover feedback); you must not create permanent state. **`preview: false`** is a commit — the caller is about to use the resolved port as an endpoint, and this is the moment a dynamic provider is allowed to create a new port.
- **`CancelResolve()`** — called when the drag is aborted. Your job is to roll back the most recent non-preview resolve, if it produced new state. No-op if you never create state.
- **`PortAdded` / `PortRemoved`** — raise whenever `Ports` changes so the canvas can invalidate its render cache. Failing to raise these leaves stale visuals on screen.

Positions in `ResolvePort` are in **node-local** coordinates — the owner node's `X` / `Y` is already subtracted. `Port.Position` is also node-local (it is derived from the port's `Anchor` and the owner's current size and shape).

### 3. Example: schema-driven port provider

A node representing a record type should expose one input port per field, laid out vertically on the left edge. The schema is known to the consumer but not to the library.

```csharp
using Avalonia;
using NodiumGraph.Model;

public sealed class SchemaPortProvider : IPortProvider
{
    private readonly Node _owner;
    private readonly List<Port> _ports = new();
    private readonly double _hitRadius;

    public SchemaPortProvider(Node owner, IEnumerable<FieldDefinition> schema, double hitRadius = 20.0)
    {
        _owner = owner;
        _hitRadius = hitRadius;

        var fieldList = schema.ToList();
        for (var i = 0; i < fieldList.Count; i++)
        {
            var field = fieldList[i];
            var fraction = fieldList.Count == 1 ? 0.5 : (double)i / (fieldList.Count - 1);
            var port = new Port(owner, field.Name, PortFlow.Input, PortAnchor.Left(fraction))
            {
                Label = field.Name,
                DataType = field.Type,
            };
            _ports.Add(port);
        }
    }

    public IReadOnlyList<Port> Ports => _ports;

    public Port? ResolvePort(Point position, bool preview)
    {
        Port? best = null;
        var bestDistance = _hitRadius;
        foreach (var port in _ports)
        {
            var dx = port.Position.X - position.X;
            var dy = port.Position.Y - position.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < bestDistance)
            {
                best = port;
                bestDistance = distance;
            }
        }
        return best;
    }

    public void CancelResolve()
    {
        // No-op — this provider never creates tentative ports.
    }

    public event Action<Port>? PortAdded;
    public event Action<Port>? PortRemoved;

    public void UpdateSchema(IEnumerable<FieldDefinition> newSchema)
    {
        foreach (var port in _ports.ToList())
        {
            _ports.Remove(port);
            PortRemoved?.Invoke(port);
        }
        var fieldList = newSchema.ToList();
        for (var i = 0; i < fieldList.Count; i++)
        {
            var field = fieldList[i];
            var fraction = fieldList.Count == 1 ? 0.5 : (double)i / (fieldList.Count - 1);
            var port = new Port(_owner, field.Name, PortFlow.Input, PortAnchor.Left(fraction))
            {
                Label = field.Name,
                DataType = field.Type,
            };
            _ports.Add(port);
            PortAdded?.Invoke(port);
        }
    }
}

public sealed record FieldDefinition(string Name, string Type);
```

The important details:

- **`ResolvePort` is a hit test, not a factory.** This provider never creates ports inside `ResolvePort` because its port set is governed by schema, not by drag gestures. The `preview` parameter is therefore ignored.
- **`UpdateSchema` raises `PortRemoved` and `PortAdded` for every change.** Without those events the canvas would keep the old port visuals on screen even after the underlying schema changed.
- **Anchors are node-relative.** The schema declares ports with `PortAnchor.Left(fraction)` — the left edge — without knowing the owner node's pixel dimensions or `X`/`Y`. The library derives the node-local `Position` from the anchor after each measure, and `AbsolutePosition` adds the node origin on top.

### 4. Tentative state and `CancelResolve`

If your provider creates state inside `ResolvePort` (because the user dragged onto an empty part of the boundary and you want to materialise a port there), you must:

1. Only create when `preview` is `false`.
2. Remember the most recently created port, if any, as "last created".
3. In `CancelResolve`, remove that port and raise `PortRemoved`. Clear the "last created" marker.
4. Clear the "last created" marker on any subsequent non-preview `ResolvePort` call (the drag committed; prior tentative state is no longer rollback-eligible).

`DynamicPortProvider` implements exactly this pattern — read its source in `src/NodiumGraph/Model/DynamicPortProvider.cs` if you're writing something similar.

### 5. Wire the provider on a node

```csharp
var node = new SchemaNode { Title = "Address", X = 120, Y = 200 };
node.PortProvider = new SchemaPortProvider(node, AddressSchema);
graph.AddNode(node);
```

Every node has its own `PortProvider`. You can mix provider types freely on the same graph — one node with a `FixedPortProvider`, another with a `DynamicPortProvider`, a third with a `SchemaPortProvider`.

## Gotchas

- **`ResolvePort` positions are node-local.** Do not re-subtract the owner's `X` / `Y`. If hits appear to be offset by the node origin, you're double-correcting.
- **`preview: true` must not mutate state.** The canvas calls `ResolvePort(..., preview: true)` many times during a drag just to update hover feedback. A provider that creates ports on preview will leave breadcrumbs on every hover.
- **Always raise `PortAdded` / `PortRemoved`.** The canvas caches node-container size and port visuals; without these events the cache goes stale and users see ghost ports or missing ports until the next layout pass.
- **Don't forget `CancelResolve`.** Dragging out, hovering over a fresh boundary point, then pressing Escape should leave the provider in its pre-drag state. If you create ports in non-preview `ResolvePort` and do nothing on cancel, every aborted drag leaves an orphan port behind.
- **`Port.Owner` must be the node the provider belongs to.** Creating a `Port` with a different owner leads to coordinate confusion and broken routing — `AbsolutePosition` uses the owner for its origin.
- **`DataType` is `object?`, not `string`.** The schema example uses strings for readability, but anything that supports `Equals` works. Be consistent across the whole graph — a provider that emits `string` ports cannot connect to another provider's `enum`-typed ports even if the names match.
## See also

- [Strategy interfaces reference](../3-reference/strategies.md#iportprovider)
- [Model reference](../3-reference/model.md)
- [Subclass Node / Connection for domain data](subclass-model.md)
- [Style ports](style-ports.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
