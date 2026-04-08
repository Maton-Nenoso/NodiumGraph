namespace NodiumGraph;

public interface INodeInteractionHandler
{
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);
    void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections);
    void OnNodeDoubleClicked(Node node);
}
