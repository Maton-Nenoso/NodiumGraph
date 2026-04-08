namespace NodiumGraph;

public interface IConnectionHandler
{
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
