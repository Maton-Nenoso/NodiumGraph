using Avalonia;

namespace NodiumGraph.Model;

/// <summary>
/// A connection endpoint on a node. Position is relative to the node's top-left corner.
/// </summary>
public class Port
{
    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public string Name { get; }
    public PortFlow Flow { get; }
    public Point Position { get; init; }

    /// <summary>
    /// World-space position computed from the owner node's location and this port's relative position.
    /// </summary>
    public Point AbsolutePosition => new(Owner.X + Position.X, Owner.Y + Position.Y);

    public Port(Node owner, string name, PortFlow flow, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Flow = flow;
        Position = position;
    }

    public Port(Node owner, Point position) : this(owner, string.Empty, PortFlow.Input, position)
    {
    }
}
