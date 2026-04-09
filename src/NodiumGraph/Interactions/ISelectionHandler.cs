using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

/// <summary>
/// Notified when the set of selected nodes changes.
/// </summary>
public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyList<Node> selectedNodes);
}
