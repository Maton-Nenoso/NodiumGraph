using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace NodiumGraph.Tests;

internal class TestSelectionHandler(Action<IReadOnlyList<Node>> callback) : ISelectionHandler
{
    public void OnSelectionChanged(IReadOnlyCollection<IGraphElement> selected)
    {
        var nodes = selected.OfType<Node>().ToList();
        callback(nodes);
    }
}

internal class TestNodeHandler : INodeInteractionHandler
{
    private readonly Action<IReadOnlyList<NodeMoveInfo>>? _onMoved;
    private readonly Action<Node>? _onDoubleClick;

    public TestNodeHandler(
        Action<IReadOnlyList<NodeMoveInfo>>? onMoved = null,
        Action<Node>? onDoubleClick = null)
    {
        _onMoved = onMoved;
        _onDoubleClick = onDoubleClick;
    }

    public void OnNodesMoved(IReadOnlyList<NodeMoveInfo> moves) => _onMoved?.Invoke(moves);
    public void OnNodeDoubleClicked(Node node) => _onDoubleClick?.Invoke(node);
}

internal class TestConnectionHandler : IConnectionHandler
{
    private readonly Func<Port, Port, Result<Connection>>? _onRequested;
    private readonly Action<Connection>? _onDeleteRequested;

    public TestConnectionHandler(
        Func<Port, Port, Result<Connection>>? onRequested = null,
        Action<Connection>? onDeleteRequested = null)
    {
        _onRequested = onRequested;
        _onDeleteRequested = onDeleteRequested;
    }

    public Result<Connection> OnConnectionRequested(Port source, Port target) =>
        _onRequested?.Invoke(source, target) ?? new Error("No handler");
    public void OnConnectionDeleteRequested(Connection connection) => _onDeleteRequested?.Invoke(connection);
}
