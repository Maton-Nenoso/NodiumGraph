namespace NodiumGraph.Model;

/// <summary>
/// Marker interface implemented by types that participate in <see cref="Graph"/> selection:
/// <see cref="Node"/> and <see cref="Connection"/>. Used by <c>Graph.SelectedItems</c> and
/// <c>IGraphInteractionHandler.OnDeleteRequested</c> for unified node+connection selection.
/// </summary>
public interface IGraphElement
{
}
