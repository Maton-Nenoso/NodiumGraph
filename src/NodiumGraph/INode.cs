namespace NodiumGraph;

/// <summary>
/// Represents a node in the graph. Implemented by consumer types.
/// </summary>
public interface INode
{
    Guid Id { get; }
    double X { get; set; }
    double Y { get; set; }
    double Width { get; }
    double Height { get; }
}
