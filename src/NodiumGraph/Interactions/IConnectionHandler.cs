using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

/// <summary>
/// Receives connection lifecycle events. Return a <see cref="Result{T}"/> from
/// <see cref="OnConnectionRequested"/> to accept or reject the connection.
/// </summary>
public interface IConnectionHandler
{
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
