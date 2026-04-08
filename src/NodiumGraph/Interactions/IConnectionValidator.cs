using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
