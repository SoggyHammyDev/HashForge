using System.Net;
using HashForge.Stratum.Transport;

namespace HashForge.Stratum.Sessions;

/// <summary>
/// The wire to one miner. Messages are written one at a time, so lines never interleave, and a miner
/// that stops reading cannot hold senders up forever.
/// </summary>
public sealed class MinerChannel(IStratumConnection connection, TimeSpan sendTimeout) : IAsyncDisposable
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public IPAddress RemoteAddress => connection.RemoteAddress;

    public Task<string?> ReceiveAsync(CancellationToken cancellationToken) => connection.ReadLineAsync(cancellationToken);

    public Task SendAsync(string message, CancellationToken cancellationToken) => SendAsync(() => message, cancellationToken);

    /// <summary>
    /// Sends the message that <paramref name="compose"/> produces once this sender holds the wire, or nothing
    /// when it returns null. Deciding under the lock is what lets callers keep messages in a strict order.
    /// </summary>
    public async Task SendAsync(Func<string?> compose, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compose);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(sendTimeout);
        await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            if (compose() is { } message)
            {
                await connection.WriteLineAsync(message, timeout.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }
}