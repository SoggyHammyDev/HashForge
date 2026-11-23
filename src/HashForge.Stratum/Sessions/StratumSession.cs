using System.Collections.Concurrent;
using System.Net;
using HashForge.Domain;
using HashForge.Stratum.Transport;

namespace HashForge.Stratum.Sessions;

/// <summary>Everything the pool knows about one miner connection.</summary>
public sealed class StratumSession : IAsyncDisposable
{
    /// <summary>Enough for a proxy fronting a rack, while keeping one connection from inventing endless workers.</summary>
    public const int MaxWorkers = 32;

    private readonly IStratumConnection _connection;
    private readonly TimeSpan _sendTimeout;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, WorkerIdentity> _workers = new(StringComparer.Ordinal);
    private volatile string? _minerAddress;
    private long _accepted;
    private long _rejected;

    public StratumSession(
        PoolDefinition pool,
        IStratumConnection connection,
        byte[] extranonce1,
        VarDiffController varDiff,
        DateTimeOffset connectedAt,
        TimeSpan sendTimeout)
    {
        _sendTimeout = sendTimeout;
        ArgumentNullException.ThrowIfNull(extranonce1);
        _connection = connection;
        Pool = pool;
        Extranonce1 = extranonce1;
        VarDiff = varDiff;
        ConnectedAt = connectedAt;
        Id = Convert.ToHexStringLower(extranonce1);
    }

    public string Id { get; }

    public PoolDefinition Pool { get; }

    public byte[] Extranonce1 { get; }

    public VarDiffController VarDiff { get; }

    public DateTimeOffset ConnectedAt { get; }

    public IPAddress RemoteAddress => _connection.RemoteAddress;

    public bool IsSubscribed { get; set; }

    /// <summary>The version-rolling mask agreed with the miner; zero until negotiated.</summary>
    public uint VersionMask { get; set; }

    /// <summary>The payout address this connection mines for, once a worker has been authorised.</summary>
    public string? MinerAddress => _minerAddress;

    /// <summary>
    /// Records an authorised worker. A connection mines for a single payout address,
    /// so a worker for a different address is refused.
    /// </summary>
    public AuthorizeOutcome Authorize(string username, WorkerIdentity worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        var address = Interlocked.CompareExchange(ref _minerAddress, worker.Address, null) ?? worker.Address;
        if (address != worker.Address)
        {
            return AuthorizeOutcome.DifferentAddress;
        }

        if (_workers.Count >= MaxWorkers && !_workers.ContainsKey(username))
        {
            return AuthorizeOutcome.TooManyWorkers;
        }

        _workers[username] = worker;
        return AuthorizeOutcome.Authorized;
    }

    public WorkerIdentity? FindWorker(string username) => _workers.GetValueOrDefault(username);

    public void CountShare(bool accepted)
    {
        if (accepted)
        {
            Interlocked.Increment(ref _accepted);
        }
        else
        {
            Interlocked.Increment(ref _rejected);
        }
    }

    public Task<string?> ReceiveAsync(CancellationToken cancellationToken) => _connection.ReadLineAsync(cancellationToken);

    /// <summary>
    /// Sends one message; concurrent senders are queued so lines never interleave. A miner that stops
    /// reading cannot hold senders up forever: the send is abandoned after the configured timeout.
    /// </summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_sendTimeout);
        await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            await _connection.WriteLineAsync(message, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public SessionInfo ToInfo() => new(
        Id,
        Pool.Name,
        RemoteAddress,
        MinerAddress,
        [.. _workers.Values.Select(w => w.WorkerName).Distinct().Order(StringComparer.Ordinal)],
        VarDiff.Difficulty,
        Interlocked.Read(ref _accepted),
        Interlocked.Read(ref _rejected),
        ConnectedAt);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }
}
