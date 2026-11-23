using System.Net.Sockets;

namespace HashForge.Stratum.Transport;

public interface IConnectionListenerFactory
{
    /// <summary>Starts listening on a port. Throws <see cref="SocketException"/> when the port is unavailable.</summary>
    IConnectionListener Create(int port);
}
