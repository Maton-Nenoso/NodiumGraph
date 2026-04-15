using NodiumGraph.Model;

namespace NodiumGraph.Interactions;

/// <summary>
/// Notified when the set of selected graph elements changes. The callback receives
/// the full selection including both <see cref="Node"/> and <see cref="Connection"/>
/// entries; consumers that only care about one kind can filter with
/// <c>selected.OfType&lt;Node&gt;()</c> or <c>selected.OfType&lt;Connection&gt;()</c>.
/// </summary>
public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected);
}
