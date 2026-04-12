using System.Collections.ObjectModel;

namespace NodiumGraph.Model;

/// <summary>
/// Container for nodes and connections that enforces structural invariants.
/// Mutate via AddNode/RemoveNode and AddConnection/RemoveConnection — the exposed
/// collections are read-only but observable for UI binding.
/// </summary>
public class Graph
{
    private readonly ObservableCollection<Node> _nodes = new();
    private readonly ObservableCollection<Connection> _connections = new();
    private readonly List<Node> _selectedNodes = new();
    private readonly HashSet<Node> _selectedSet = new();
    private readonly ReadOnlyCollection<Node> _selectedNodesReadOnly;

    public ReadOnlyObservableCollection<Node> Nodes { get; }
    public ReadOnlyObservableCollection<Connection> Connections { get; }

    public Graph()
    {
        Nodes = new(_nodes);
        Connections = new(_connections);
        _selectedNodesReadOnly = _selectedNodes.AsReadOnly();
    }

    public IReadOnlyList<Node> SelectedNodes => _selectedNodesReadOnly;

    public void AddNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_nodes.Contains(node))
            throw new InvalidOperationException("Node is already in the graph.");
        _nodes.Add(node);
    }

    /// <summary>
    /// Removes a node and cascades to any connections referencing its ports.
    /// </summary>
    public void RemoveNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var toRemove = _connections
            .Where(c => c.SourcePort.Owner == node || c.TargetPort.Owner == node)
            .ToList();

        foreach (var conn in toRemove)
            _connections.Remove(conn);

        node.IsSelected = false;
        _selectedSet.Remove(node);
        _selectedNodes.Remove(node);
        _nodes.Remove(node);
    }

    /// <summary>
    /// Removes multiple nodes and all connections referencing their ports in a single pass.
    /// Nodes in the input sequence that are not part of this graph are silently skipped.
    /// </summary>
    public void RemoveNodes(IEnumerable<Node> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var nodeSet = new HashSet<Node>(nodes);
        if (nodeSet.Count == 0) return;

        var connectionsToRemove = _connections
            .Where(c => nodeSet.Contains(c.SourcePort.Owner) || nodeSet.Contains(c.TargetPort.Owner))
            .ToList();

        foreach (var conn in connectionsToRemove)
            _connections.Remove(conn);

        foreach (var node in nodeSet)
        {
            node.IsSelected = false;
            _selectedSet.Remove(node);
            _selectedNodes.Remove(node);
            _nodes.Remove(node);
        }
    }

    public void AddConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (_connections.Contains(connection))
            throw new InvalidOperationException("Connection is already in the graph.");
        if (!_nodes.Contains(connection.SourcePort.Owner))
            throw new InvalidOperationException("Source port's owner node is not in the graph.");
        if (!_nodes.Contains(connection.TargetPort.Owner))
            throw new InvalidOperationException("Target port's owner node is not in the graph.");
        _connections.Add(connection);
    }

    /// <summary>
    /// Removes a connection. No-op if the connection is not in the graph (idempotent).
    /// </summary>
    public void RemoveConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connections.Remove(connection);
    }

    public void Select(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Contains(node))
            throw new InvalidOperationException("Node is not part of this graph.");
        if (_selectedSet.Add(node))
        {
            _selectedNodes.Add(node);
            node.IsSelected = true;
        }
    }

    /// <summary>
    /// Deselects a node. No-op if the node is not selected (idempotent).
    /// </summary>
    public void Deselect(Node node)
    {
        if (_selectedSet.Remove(node))
        {
            node.IsSelected = false;
            _selectedNodes.Remove(node);
        }
    }

    public void ClearSelection()
    {
        foreach (var node in _selectedNodes)
            node.IsSelected = false;
        _selectedNodes.Clear();
        _selectedSet.Clear();
    }
}
