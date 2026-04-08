namespace NodiumGraph;

public interface ISelectionHandler
{
    void OnSelectionChanged(IReadOnlyList<Node> selectedNodes);
}
