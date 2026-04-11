using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NodiumGraph.Model;

public class GroupNode : Node
{
    private readonly ObservableCollection<Node> _children = new();
    private double _lastX;
    private double _lastY;

    public ReadOnlyObservableCollection<Node> Children { get; }

    public GroupNode()
    {
        Children = new(_children);
        _lastX = X;
        _lastY = Y;
        PropertyChanged += OnSelfPropertyChanged;
    }

    private void OnSelfPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(X))
        {
            var delta = X - _lastX;
            _lastX = X;
            foreach (var child in _children)
                child.X += delta;
        }
        else if (e.PropertyName == nameof(Y))
        {
            var delta = Y - _lastY;
            _lastY = Y;
            foreach (var child in _children)
                child.Y += delta;
        }
    }

    public void AddChild(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_children.Contains(node))
            _children.Add(node);
    }

    public void RemoveChild(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _children.Remove(node);
    }
}
