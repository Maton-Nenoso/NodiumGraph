using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

/// <summary>
/// Receives node-level interactions (move, double-click).
/// </summary>
public interface INodeInteractionHandler
{
    void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves);
    void OnNodeDoubleClicked(Node node);
}
