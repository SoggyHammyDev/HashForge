using System.Net;
using System.Net.Sockets;

namespace HashForge.Stratum.Transport;

public sealed class TcpConnectionListener : IConnectionListener
{
    private readonly TcpListener _listener;
    private readonly int _maxLineLength;

    public TcpConnectionListener(int port, int maxLineLength)
    {
        _maxLineLength = maxLineLength;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async Task<IStratumConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        return new TcpStratumConnection(client, _maxLineLength);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }
}
