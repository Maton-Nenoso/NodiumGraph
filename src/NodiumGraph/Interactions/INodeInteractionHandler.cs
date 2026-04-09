using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

/// <summary>
/// Receives node-level interactions (move, delete, double-click).
/// </summary>
public interface INodeInteractionHandler
{
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);
    void OnDeleteRequested(IReadOnlyList<Node> nodes, IReadOnlyList<Connection> connections);
    void OnNodeDoubleClicked(Node node);
}
