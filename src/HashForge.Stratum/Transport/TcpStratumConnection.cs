using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HashForge.Stratum.Transport;

public sealed class TcpStratumConnection : IStratumConnection
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly LineReader _reader;

    public TcpStratumConnection(TcpClient client, int maxLineLength)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _reader = new LineReader(_stream, maxLineLength);
        RemoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
    }

    public IPAddress RemoteAddress { get; }

    public Task<string?> ReadLineAsync(CancellationToken cancellationToken) =>
        _reader.ReadLineAsync(cancellationToken).AsTask();

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken) =>
        await _stream.WriteAsync(Encoding.UTF8.GetBytes(line + '\n'), cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
    }
}
