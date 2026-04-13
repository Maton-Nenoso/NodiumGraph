# Write a Custom IConnectionValidator

## Goal

Decide — per-drag, in real time — which source/target port pairs are legal connections in your graph. Reasons range from "this would create a cycle" to "this target already has an incoming edge" to "these two data types can't be mixed".

## Prerequisites

- You already host `NodiumGraphCanvas` and have `Node`s with ports. See [Host the Canvas](host-canvas.md).
- You've read how [DefaultConnectionValidator](../3-reference/strategies.md#built-in-defaultconnectionvalidator) already handles the structural cases (self, same-owner, same-flow, mismatched `DataType`). Most custom validators should **layer on top of it**, not replace it.

## Steps

### 1. The contract

```csharp
public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
```

- Called during a connection drag whenever the pointer moves over a candidate target port. Returning `true` turns the live drag preview into the "accept" visual; returning `false` switches it to "reject" and suppresses `IConnectionHandler.OnConnectionRequested` entirely when the user releases.
- Must be fast — a few dozen times per second per pointer-move burst. Avoid allocations.
- Must be deterministic and stateless *for the same inputs and graph state*. The canvas does not cache validator results between calls.
- Set `NodiumGraphCanvas.ConnectionValidator` to `null` to bypass validation altogether (not recommended — see gotchas).

### 2. Start from the default, don't discard it

`DefaultConnectionValidator.Instance` is a singleton with the following rules applied in order:

1. Reject if `source == target` (same port).
2. Reject if `source.Owner == target.Owner` (both ports on the same node).
3. Reject if `source.Flow == target.Flow` (output→output or input→input).
4. Accept iff `Equals(source.DataType, target.DataType)`. `null` matches only `null` — it is **not** a wildcard.

You almost always want your validator to delegate to the default first and then add your own checks on top. If you rewrite the whole thing, you become responsible for re-implementing the structural rules above.

```csharp
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class SingleIncomingValidator(Graph graph) : IConnectionValidator
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

### 3. Reject cycles

For directed-acyclic graphs (pipelines, compute DAGs, state machines), reject any attempt that would create a back edge. A cycle check is a DFS from the prospective target following outgoing edges, looking for the source's owner.

```csharp
public sealed class DagValidator(Graph graph) : IConnectionValidator
{
    public bool CanConnect(Port source, Port target)
    {
        if (!DefaultConnectionValidator.Instance.CanConnect(source, target))
            return false;

        // Would this create a path target.Owner -> ... -> source.Owner?
        return !CanReach(target.Owner, source.Owner);
    }

    private bool CanReach(Node from, Node to)
    {
        var stack = new Stack<Node>();
        var seen = new HashSet<Node>();
        stack.Push(from);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == to) return true;
            if (!seen.Add(current)) continue;

            foreach (var c in graph.Connections)
            {
                if (c.SourcePort.Owner == current)
                    stack.Push(c.TargetPort.Owner);
            }
        }
        return false;
    }
}
```

This runs inside the pointer-move callback, so on very large graphs (tens of thousands of edges) you'll want to cache or index the outbound-edge map instead of scanning `graph.Connections` every call.

### 4. Type-compatibility rules richer than `DataType`

`DataType` is a deliberately simple opaque equality check — two ports connect iff their `DataType` values are `Equals`-equal. If you need subtype compatibility (`int` flows into `number`), covariance, or a type lattice, layer it in your validator and keep using `DataType` as the tag:

```csharp
public sealed class TypeLatticeValidator(Func<string?, string?, bool> isAssignableTo) : IConnectionValidator
{
    public bool CanConnect(Port source, Port target)
    {
        // Keep structural checks from the default, but override the DataType step.
        if (ReferenceEquals(source, target)) return false;
        if (source.Owner == target.Owner) return false;
        if (source.Flow == target.Flow) return false;

        // Your assignability predicate replaces the default's DataType equality check.
        var sourceType = source.DataType as string;
        var targetType = target.DataType as string;
        return isAssignableTo(sourceType, targetType);
    }
}
```

This is the one case where you cannot just delegate to `DefaultConnectionValidator.Instance` — its step 4 would reject a legal subtype pair before you get a chance to say yes.

### 5. Wire it on the canvas

```csharp
Canvas.ConnectionValidator = new SingleIncomingValidator(graph);
```

Or bypass validation entirely:

```csharp
Canvas.ConnectionValidator = null;
```

Assignments take effect immediately and are re-consulted on the next pointer move.

## Full code

```csharp
using NodiumGraph.Interactions;
using NodiumGraph.Model;

public sealed class AppConnectionValidator(Graph graph) : IConnectionValidator
{
    public bool CanConnect(Port source, Port target)
    {
        // 1. Structural + DataType rules from the library.
        if (!DefaultConnectionValidator.Instance.CanConnect(source, target))
            return false;

        // 2. At most one incoming per target port.
        foreach (var c in graph.Connections)
        {
            if (c.TargetPort == target) return false;
        }

        // 3. No cycles (DAG constraint).
        if (CanReach(target.Owner, source.Owner, graph)) return false;

        return true;
    }

    private static bool CanReach(Node from, Node to, Graph graph)
    {
        var stack = new Stack<Node>();
        var seen = new HashSet<Node>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == to) return true;
            if (!seen.Add(current)) continue;
            foreach (var c in graph.Connections)
            {
                if (c.SourcePort.Owner == current)
                    stack.Push(c.TargetPort.Owner);
            }
        }
        return false;
    }
}
```

## Gotchas

- **`DefaultConnectionValidator.Instance` is a singleton — don't replace it, delegate to it.** Rewriting the structural checks is the most common bug; a custom validator that forgets to reject same-owner pairs will happily let users loop a node back to itself.
- **`CanConnect` is called on the UI thread, many times per second.** Avoid LINQ in hot loops, avoid allocating closures, and never hit the disk or network. Cache anything non-trivial and invalidate it when `graph.Connections` changes.
- **Rejecting here does not delete anything.** Validation only affects the *live drag preview* and the decision to call `IConnectionHandler.OnConnectionRequested`. An already-existing connection that would now fail validation is untouched — if you change the rules at runtime, walk the graph separately and clean up.
- **`Port.DataType` is `object?`, not `string`.** Equality is whatever `Equals` says for your chosen types. If you put strings on one side and enums on the other, they will never match. Pick one and stick with it.
- **`null` is not a wildcard.** A port with no `DataType` only connects to other ports with no `DataType`. This is documented as deliberate in the [strategies reference](../3-reference/strategies.md#built-in-defaultconnectionvalidator) — if you want permissive behaviour, special-case `null` in your own validator.
- **Setting `ConnectionValidator = null` disables all feedback.** The drag preview always shows as "valid" and every release fires `OnConnectionRequested`. Your handler still gets a final veto via `Result<Connection>`, but the user experience is worse — they only discover the rejection at release time, not during hover.
- **Validators run against an in-progress graph.** If your handler adds nodes or connections during a drag (it shouldn't, but it can), the validator sees the mutated state. Keep the handler and the validator consistent.

## See also

- [Strategy interfaces reference](../3-reference/strategies.md#iconnectionvalidator)
- [Handler interfaces reference](../3-reference/handlers.md)
- [Result pattern reference](../3-reference/result-pattern.md)
- [Model reference](../3-reference/model.md)
- [Report, don't decide](../4-explanation/report-dont-decide.md)
