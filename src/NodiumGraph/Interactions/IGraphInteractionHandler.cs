using System.Collections.Generic;
using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Cross-cutting handler for canvas-wide interactions that may target a mix of
/// nodes and connections, e.g. the delete key firing with a heterogeneous selection.
/// See <see cref="INodeInteractionHandler"/> for per-node interaction events
/// and <see cref="IConnectionHandler"/> for per-connection interaction events.
/// </summary>
public interface IGraphInteractionHandler
{
    /// <summary>
    /// The user requested deletion of the given elements. Elements may be a mix
    /// of <see cref="Node"/>s and <see cref="Connection"/>s. The consumer decides
    /// removal order and whether to group the operation into a single undo batch.
    /// The library does NOT remove the elements from the graph — the consumer is
    /// responsible for calling <c>Graph.RemoveNode</c> / <c>Graph.RemoveConnection</c>.
    /// </summary>
    void OnDeleteRequested(IReadOnlyCollection<IGraphElement> elements);
}
