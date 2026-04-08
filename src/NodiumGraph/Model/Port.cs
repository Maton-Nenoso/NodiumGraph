using Avalonia;

namespace NodiumGraph.Model;

public class Port
{
    public Guid Id { get; } = Guid.NewGuid();
    public Node Owner { get; }
    public Point Position { get; set; }

    public Point AbsolutePosition => new(Owner.X + Position.X, Owner.Y + Position.Y);

    public Port(Node owner, Point position)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Position = position;
    }
}
