---
title: Persist and Restore Graph State
tags: [how-to]
status: active
created: 2026-04-13
updated: 2026-05-19
---

# Persist and Restore Graph State

## Goal

Save a `Graph` to disk (or any other medium) and reload it later into a fresh `NodiumGraphCanvas`.

This is a **pointer recipe**. NodiumGraph deliberately does not ship a serializer — the format is yours, the library only shows you which seams to hook into.

## Prerequisites

- You already host `NodiumGraphCanvas` and know how to build a `Graph` in code. See [Host the Canvas](host-canvas.md) and [Getting Started](../1-tutorial/getting-started.md).
- You understand the model types. See [Model reference](../3-reference/model.md).

## Steps

### 1. Why the library doesn't do this for you

Every app has its own opinion on serialization. Some want JSON, some MessagePack, some a SQL schema, some a round-trip format they already own. NodiumGraph refuses to pick one — see [Report, don't decide](../4-explanation/report-dont-decide.md) — so that your save format is whatever is best for *your* product, not what the library could agree on.

The model is built to make this easy: `Node`, `Port`, and `Connection` are concrete classes with public properties and stable string `Id`s. There is no hidden state, no internal-only field that a serializer needs to reach. If you can round-trip the list of public properties, you can round-trip the graph.

### 2. What you need to capture

To fully round-trip a graph, you need **these fields, and only these fields**:

**Per `Node`:**
- `Id` — stable identifier. The default constructor assigns a GUID; if you persist, you want to control this yourself so the same logical node gets the same id across saves.
- `Title`, `X`, `Y` — base fields users can see and move.
- Any properties added by your own subclass (`Description`, `Formula`, `Value`, whatever). These are the reason you subclassed `Node` in the first place.
- The node's `PortProvider` — specifically, which ports it has, each port's `PortAnchor` (`Edge` + `Fraction`), `PortFlow`, `Label`, and `DataType`.

You do **not** need to persist:
- `Width` / `Height` — set by the canvas after it measures the template. Restoring a node re-measures it.
- `ShowHeader`, `IsCollapsed`, `Style` — only persist these if your UX lets the user toggle them.
- `Port.Position` or `Port.AbsolutePosition` — both are derived from the anchor and the node's current geometry. Persist `Anchor.Edge` + `Anchor.Fraction` instead.

**Per `Connection`:**
- `Id`
- The `Id` of its source node, the `Id` of its source port, the `Id` of its target node, and the `Id` of its target port. That's enough to look everything up on load.
- Any properties on your own `Connection` subclass.

**Viewport (optional):**
- `Canvas.ViewportZoom` and `Canvas.ViewportOffset` if you want "reopen the file with the same camera pose". Both live on the canvas, not on the graph, so persist them alongside the graph, not inside it.

### 3. Design the file format with round-tripping in mind

A deliberately minimal JSON shape:

```json
{
  "version": 1,
  "viewport": { "zoom": 1.0, "offsetX": 0, "offsetY": 0 },
  "nodes": [
    {
      "id": "n-add-1",
      "kind": "MathNode",
      "title": "Add",
      "x": 120, "y": 200,
      "description": "a + b",
      "ports": [
        { "id": "in-a",  "flow": "Input",  "anchor": { "edge": "Left",  "fraction": 0.25 }, "label": "a",      "dataType": "number" },
        { "id": "in-b",  "flow": "Input",  "anchor": { "edge": "Left",  "fraction": 0.75 }, "label": "b",      "dataType": "number" },
        { "id": "out",   "flow": "Output", "anchor": { "edge": "Right", "fraction": 0.5  }, "label": "result", "dataType": "number" }
      ]
    }
  ],
  "connections": [
    { "id": "c1", "sourceNode": "n-add-1", "sourcePort": "out", "targetNode": "n-sink-1", "targetPort": "in" }
  ]
}
```

A few deliberate choices:

- **Explicit `version`.** The format will grow; start versioned on day one.
- **`kind` discriminator on nodes.** Serialization needs to know which `Node` subclass to instantiate on load. A plain string is enough.
- **Ids on ports are node-local.** Port ids only need to be unique within their owning node — connections reference them as a `(nodeId, portId)` pair. This keeps node-level refactors from rippling into connection data.
- **Persist `anchor`, not `x`/`y`.** `Port.Position` and `Port.AbsolutePosition` are derived values — they change when a node resizes. The `anchor` (`edge` + `fraction`) is the stable, size-independent declaration. On load, pass it directly to the `Port` constructor and the library recomputes the pixel position after measure.
- **Node `x`/`y` are world coordinates.** Do not store `ViewportZoom`-scaled screen values for those — they are world units, same as you pass to the `Node` constructor.

### 4. Write the serializer

Walk `graph.Nodes` and `graph.Connections` and project each into your DTO. For `PortProvider`, the easy case is a `FixedPortProvider` — iterate `Ports` and record each port's `Anchor.Edge`, `Anchor.Fraction`, `Flow`, `Label`, `DataType`, and `Id`. `DynamicPortProvider` is trickier: decide whether you persist the resolved ports (using the same anchor fields) or only the provider configuration and let the provider recreate ports on the first connection attempt after load.

```csharp
public static GraphDto ToDto(Graph graph, NodiumGraphCanvas canvas)
{
    var nodes = graph.Nodes.Select(n => new NodeDto
    {
        Id = n.Id,
        Kind = n.GetType().Name,
        Title = n.Title,
        X = n.X,
        Y = n.Y,
        Description = (n as MathNode)?.Description,
        Ports = SerializePorts(n.PortProvider),
    }).ToList();

    var connections = graph.Connections.Select(c => new ConnectionDto
    {
        Id = c.Id,
        SourceNode = c.SourcePort.Owner.Id,
        SourcePort = c.SourcePort.Id,
        TargetNode = c.TargetPort.Owner.Id,
        TargetPort = c.TargetPort.Id,
    }).ToList();

    return new GraphDto
    {
        Version = 1,
        Viewport = new ViewportDto(canvas.ViewportZoom, canvas.ViewportOffset.X, canvas.ViewportOffset.Y),
        Nodes = nodes,
        Connections = connections,
    };
}
```

### 5. Write the deserializer

The load path has two passes: build all nodes (and their ports) first, then build the connections. This separation exists because `Connection` holds port references, and you cannot create connections before both endpoints exist.

```csharp
public static Graph FromDto(GraphDto dto)
{
    var graph = new Graph();
    var nodeLookup = new Dictionary<string, Node>();

    // Pass 1: nodes + ports
    foreach (var nDto in dto.Nodes)
    {
        var node = CreateNode(nDto);  // instantiates the right subclass by Kind
        graph.AddNode(node);
        nodeLookup[node.Id] = node;
    }

    // Pass 2: connections
    foreach (var cDto in dto.Connections)
    {
        var sourceNode = nodeLookup[cDto.SourceNode];
        var targetNode = nodeLookup[cDto.TargetNode];
        var sourcePort = sourceNode.PortProvider!.Ports.First(p => p.Id == cDto.SourcePort);
        var targetPort = targetNode.PortProvider!.Ports.First(p => p.Id == cDto.TargetPort);
        graph.AddConnection(new Connection(sourcePort, targetPort) { Id = cDto.Id });
    }

    return graph;
}
```

`CreateNode` is yours — a plain `switch` on `Kind` that news up the right subclass and attaches a `FixedPortProvider` populated from the DTO. For each port DTO, reconstruct the anchor as `new PortAnchor(Enum.Parse<PortEdge>(dto.Edge), dto.Fraction)` and pass it to `new Port(node, dto.Id, Enum.Parse<PortFlow>(dto.Flow), anchor)`. The canvas recomputes `Position` and `AbsolutePosition` after measuring the node template — no world-unit coordinates needed at load time.

### 6. Wire loading into the canvas

After deserializing, assign `Canvas.Graph = graph` and, if you saved it, restore the viewport. Do this before the first layout pass — typically from the window constructor or from an async load command running on the UI thread:

```csharp
Canvas.Graph = FromDto(dto);
Canvas.ViewportZoom = dto.Viewport.Zoom;
Canvas.ViewportOffset = new Point(dto.Viewport.OffsetX, dto.Viewport.OffsetY);
```

## Gotchas

- **Generate stable `Id`s yourself.** The default `Node.Id` / `Port.Id` / `Connection.Id` is a new GUID per instance. If you do not override it during load, the restored graph will not match the file you just read — any *external* system that remembered those ids will point at nothing.
- **Do not persist `Node.Width` / `Node.Height`.** The canvas writes them after measuring the DataTemplate. Persisted values will be overwritten the first time the canvas lays out the restored graph.
- **Add ports before constructing connections.** A `Connection` validates that both endpoints are non-null at construction time; if the port hasn't been attached to its node yet, you'll be fishing for references that don't exist.
- **Respect the two-pass load order.** Nodes first, then connections. Interleaving them forces partial state the rest of the library does not expect.
- **Use the same `PortFlow` on both ends.** `DefaultConnectionValidator` will reject Output→Output or Input→Input pairings on `graph.AddConnection`. Persisting a graph built before that validator existed may contain invalid pairs — decide at load whether to drop them, fix them, or upgrade the file.
- **`DynamicPortProvider` needs more care.** Unlike `FixedPortProvider`, its ports are created at runtime in response to connection attempts. Decide whether to snapshot the current port set (simple) or persist only the configuration and let the provider recreate ports on the first connection attempt after load (cleaner, but user-visible port identities change).
- **Assembly-qualified type names are a trap.** Storing `n.GetType().FullName` locks your save format to your current assembly layout. A short `Kind` string you own is safer across refactors.
- **Versioning is non-optional.** Even a tiny app eventually adds a field. Put `version` in the root object from day one, even if it's always `1` for a while.

## Persisting ports declared with auto-layout

Auto-layout ports express an intent at registration time, not just a value on the live
`Port`. If you serialize a graph by storing only each port's `Anchor.Fraction`, auto
ports reload as **pinned at the saved fraction** — the `IsAutoFraction` bit is lost.
This silently breaks runtime re-layout: a later `AddPort` / `RemovePort` on the same
edge will skip the reloaded port (because the library believes it is pinned), and the
visual distribution drifts.

Two recommended approaches:

### 1. Persist by spec, not by port (recommended)

Store the original `PortDefinition` / `PortSpec` shape — including `null` `Fraction`
for auto ports — and rehydrate the graph by re-registering through `NodePortRegistry`,
then letting `Node.EnsureMaterialized` rebuild the ports. Layout re-runs naturally on
load.

This approach works naturally if you declare ports in AXAML: the `NodePortRegistry` is
populated when the AXAML window loads, so re-running `Node.EnsureMaterialized` on each
loaded node gets you fully-laid-out ports without storing any fraction values.

### 2. Persist `IsAutoFraction` alongside `Fraction`

If you have a custom port serializer, include the bit explicitly:

```csharp
public record SerializedPort(string Name, PortFlow Flow, PortEdge Edge,
                              double? Fraction, bool IsAutoFraction);
```

On rehydrate, branch on `IsAutoFraction`:

```csharp
var port = serialized.IsAutoFraction
    ? new Port(owner, serialized.Name, serialized.Flow, serialized.Edge)
    : new Port(owner, serialized.Name, serialized.Flow,
               new PortAnchor(serialized.Edge, serialized.Fraction!.Value));
```

Then hand the rehydrated ports to `new FixedPortProvider(ports)`. The provider's
constructor runs the layout pass; any saved auto `Fraction` is overwritten by the
distribution formula — which is the correct behavior, since the saved value was a
snapshot, not the intent.

## See also

- [Model reference](../3-reference/model.md)
- [Handler interfaces reference](../3-reference/handlers.md)
- [Strategy interfaces reference](../3-reference/strategies.md)
- [Subclass Node / Connection for domain data](subclass-model.md)
- [Custom port provider](custom-port-provider.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
