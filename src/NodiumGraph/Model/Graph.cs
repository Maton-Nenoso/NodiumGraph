using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NodiumGraph.Model;

/// <summary>
/// Container for nodes and connections that enforces structural invariants.
/// Mutate via AddNode/RemoveNode and AddConnection/RemoveConnection — the exposed
/// <see cref="Nodes"/> and <see cref="Connections"/> collections are read-only but
/// observable for UI binding.
///
/// Selection is unified: write to <see cref="SelectedItems"/> (both nodes and connections);
/// <see cref="SelectedNodes"/> and <see cref="SelectedConnections"/> are read-only views
/// that automatically mirror the relevant subset.
/// </summary>
public class Graph
{
    private readonly ObservableCollection<Node> _nodes = new();
    private readonly ObservableCollection<Connection> _connections = new();
    private readonly ObservableCollection<Node> _selectedNodes = new();
    private readonly ObservableCollection<Connection> _selectedConnections = new();

    public ReadOnlyObservableCollection<Node> Nodes { get; }
    public ReadOnlyObservableCollection<Connection> Connections { get; }

    /// <summary>
    /// Canonical selection set for both nodes and connections.
    /// Write here; <see cref="SelectedNodes"/> and <see cref="SelectedConnections"/> are
    /// read-only views that mirror the relevant subset automatically.
    ///
    /// Enforces set semantics (duplicate adds are no-ops) and graph ownership (adding an
    /// element that is not in <see cref="Nodes"/> or <see cref="Connections"/> throws
    /// <see cref="InvalidOperationException"/>).
    ///
    /// Mutations are not reentrancy-safe. Do not modify from inside a
    /// <see cref="SelectedNodes"/> / <see cref="SelectedConnections"/> / <see cref="SelectedItems"/>
    /// change handler — doing so throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public ObservableCollection<IGraphElement> SelectedItems { get; }

    /// <summary>
    /// Read-only view over the <see cref="Node"/> entries in <see cref="SelectedItems"/>.
    /// </summary>
    public ReadOnlyObservableCollection<Node> SelectedNodes { get; }

    /// <summary>
    /// Read-only view over the <see cref="Connection"/> entries in <see cref="SelectedItems"/>.
    /// </summary>
    public ReadOnlyObservableCollection<Connection> SelectedConnections { get; }

    public Graph()
    {
        Nodes = new ReadOnlyObservableCollection<Node>(_nodes);
        Connections = new ReadOnlyObservableCollection<Connection>(_connections);
        SelectedNodes = new ReadOnlyObservableCollection<Node>(_selectedNodes);
        SelectedConnections = new ReadOnlyObservableCollection<Connection>(_selectedConnections);
        SelectedItems = new GraphSelectionCollection(this);
        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }

    private bool _partitioning;

    private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_partitioning)
            throw new InvalidOperationException(
                "Do not mutate Graph.SelectedItems from a SelectedNodes/SelectedConnections change handler.");

        _partitioning = true;
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                        foreach (var item in e.NewItems) AddToViews(item);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                        foreach (var item in e.OldItems) RemoveFromViews(item);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                        foreach (var item in e.OldItems) RemoveFromViews(item);
                    if (e.NewItems != null)
                        foreach (var item in e.NewItems) AddToViews(item);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var n in _selectedNodes) n.IsSelected = false;
                    _selectedNodes.Clear();
                    _selectedConnections.Clear();
                    break;
                case NotifyCollectionChangedAction.Move:
                    // Views are unordered — nothing to do.
                    break;
            }
        }
        finally
        {
            _partitioning = false;
        }
    }

    private void AddToViews(object? item)
    {
        if (item is Node n)
        {
            _selectedNodes.Add(n);
            n.IsSelected = true;
        }
        else if (item is Connection c)
        {
            _selectedConnections.Add(c);
        }
    }

    private void RemoveFromViews(object? item)
    {
        if (item is Node n)
        {
            _selectedNodes.Remove(n);
            n.IsSelected = false;
        }
        else if (item is Connection c)
        {
            _selectedConnections.Remove(c);
        }
    }

    public void AddNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_nodes.Contains(node))
            throw new InvalidOperationException("Node is already in the graph.");
        _nodes.Add(node);
    }

    /// <summary>
    /// Removes a node and cascades to any connections referencing its ports.
    /// Cascaded connections and the node itself are also pulled out of <see cref="SelectedItems"/>.
    /// </summary>
    public void RemoveNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var toRemove = _connections
            .Where(c => c.SourcePort.Owner == node || c.TargetPort.Owner == node)
            .ToList();

        foreach (var conn in toRemove)
            SelectedItems.Remove(conn);

        foreach (var conn in toRemove)
            _connections.Remove(conn);

        SelectedItems.Remove(node);
        _nodes.Remove(node);
    }

    /// <summary>
    /// Removes multiple nodes and all connections referencing their ports in a single pass.
    /// Cascaded connections and the nodes themselves are also pulled out of <see cref="SelectedItems"/>.
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
            SelectedItems.Remove(conn);

        foreach (var conn in connectionsToRemove)
            _connections.Remove(conn);

        foreach (var node in nodeSet)
        {
            SelectedItems.Remove(node);
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
    /// Removes a connection. Also pulls it out of <see cref="SelectedItems"/> if selected.
    /// No-op if the connection is not in the graph (idempotent).
    /// </summary>
    public void RemoveConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        SelectedItems.Remove(connection);
        _connections.Remove(connection);
    }

    public void Select(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.Contains(node))
            throw new InvalidOperationException("Node is not part of this graph.");
        if (!SelectedItems.Contains(node))
            SelectedItems.Add(node);
    }

    /// <summary>
    /// Deselects a node. No-op if the node is not selected (idempotent).
    /// </summary>
    public void Deselect(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        SelectedItems.Remove(node);
    }

    public void ClearSelection()
    {
        SelectedItems.Clear();
    }

    private sealed class GraphSelectionCollection : ObservableCollection<IGraphElement>
    {
        private readonly Graph _graph;

        public GraphSelectionCollection(Graph graph) => _graph = graph;

        protected override void InsertItem(int index, IGraphElement item)
        {
            ArgumentNullException.ThrowIfNull(item);

            // Idempotent: no-op if already in the collection.
            if (Contains(item)) return;

            // Membership: must belong to the owning graph.
            bool belongs = item switch
            {
                Node n => _graph._nodes.Contains(n),
                Connection c => _graph._connections.Contains(c),
                _ => false
            };

            if (!belongs)
                throw new InvalidOperationException(
                    $"Cannot select a {item.GetType().Name} that is not part of this Graph.");

            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, IGraphElement item)
        {
            // Replace is rarely used for selection. Keep the same guards.
            ArgumentNullException.ThrowIfNull(item);

            if (!ReferenceEquals(this[index], item) && Contains(item))
                // The new item is already elsewhere in the collection — treat as a remove-at-index.
                RemoveAt(index);
            else
                base.SetItem(index, item);
        }
    }
}
