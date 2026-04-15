namespace NodiumGraph.Model;

/// <summary>
/// A directed link between two ports. Subclass to attach labels or weights.
/// </summary>
public class Connection : IGraphElement
{
    public Guid Id { get; } = Guid.NewGuid();
    public Port SourcePort { get; }
    public Port TargetPort { get; }

    public Connection(Port sourcePort, Port targetPort)
    {
        SourcePort = sourcePort ?? throw new ArgumentNullException(nameof(sourcePort));
        TargetPort = targetPort ?? throw new ArgumentNullException(nameof(targetPort));
    }
}
