namespace NodiumGraph.Model;

public class Connection
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
