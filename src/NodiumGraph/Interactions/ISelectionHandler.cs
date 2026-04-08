using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyList<Node> selectedNodes);
}
