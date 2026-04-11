using NodiumGraph.Model;
namespace NodiumGraph.Interactions;

/// <summary>
/// Receives connection lifecycle events. Return a <see cref="Result{T}"/> from
/// <see cref="OnConnectionRequested"/> to accept or reject the connection.
/// </summary>
public interface IConnectionHandler
{
    /// <summary>
    /// Called when the user completes a connection drag onto a valid target port.
    /// Return <see cref="Result{T}"/> with <c>IsSuccess = true</c> to confirm the connection was accepted.
    /// <para>
    /// <b>Note:</b> The library only checks <see cref="Result.IsSuccess"/>; it does not consume
    /// <see cref="Result{T}.Value"/>. Add the connection to your <see cref="Graph"/> inside this method.
    /// </para>
    /// </summary>
    Result<Connection> OnConnectionRequested(Port source, Port target);
    void OnConnectionDeleteRequested(Connection connection);
}
