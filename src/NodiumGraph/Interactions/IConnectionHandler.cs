using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

public interface IConnectionHandler
{
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
