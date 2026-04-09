using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

/// <summary>
/// Validates whether a connection between two ports is allowed. Called during drag for live feedback.
/// </summary>
public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
