using System.Collections.ObjectModel;

namespace NodiumGraph.Model;

public class GroupNode : Node
{
    private readonly ObservableCollection<Node> _children = new();

    public ReadOnlyObservableCollection<Node> Children { get; }

    public GroupNode()
    {
        Children = new(_children);
    }

    public void AddChild(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_children.Contains(node))
            _children.Add(node);
    }

    public void RemoveChild(Node node)
    {
        _children.Remove(node);
    }
}
