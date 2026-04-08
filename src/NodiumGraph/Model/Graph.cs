using System.Collections.ObjectModel;

namespace NodiumGraph.Model;

public class Graph
{
    private readonly List<Node> _selectedNodes = new();

    public ObservableCollection<Node> Nodes { get; } = new();
    public ObservableCollection<Connection> Connections { get; } = new();
    public IReadOnlyList<Node> SelectedNodes => _selectedNodes;

    public void AddNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Nodes.Contains(node))
            throw new InvalidOperationException("Node is already in the graph.");
        Nodes.Add(node);
    }

    public void RemoveNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var toRemove = Connections
            .Where(c => c.SourcePort.Owner == node || c.TargetPort.Owner == node)
            .ToList();

        foreach (var conn in toRemove)
            Connections.Remove(conn);

        _selectedNodes.Remove(node);
        Nodes.Remove(node);
    }

    public void AddConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (Connections.Contains(connection))
            throw new InvalidOperationException("Connection is already in the graph.");
        Connections.Add(connection);
    }

    public void RemoveConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connections.Remove(connection);
    }

    public void Select(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!Nodes.Contains(node))
            throw new InvalidOperationException("Node is not part of this graph.");
        if (!_selectedNodes.Contains(node))
            _selectedNodes.Add(node);
    }

    public void Deselect(Node node)
    {
        _selectedNodes.Remove(node);
    }

    public void ClearSelection()
    {
        _selectedNodes.Clear();
    }
}
