using System.Collections.Concurrent;
using HashForge.Domain;

namespace HashForge.Stratum.Sessions;

/// <summary>The workers one connection has authorised. A connection mines for a single payout address.</summary>
public sealed class WorkerRoster
{
    /// <summary>Enough for a proxy fronting a rack, while keeping one connection from inventing endless workers.</summary>
    public const int MaxWorkers = 32;

    private readonly ConcurrentDictionary<string, WorkerIdentity> _workers = new(StringComparer.Ordinal);
    private volatile string? _minerAddress;

    /// <summary>The payout address this connection mines for, once a worker has been authorised.</summary>
    public string? MinerAddress => _minerAddress;

    public IReadOnlyList<string> WorkerNames => [.. _workers.Values.Select(w => w.WorkerName).Distinct().Order(StringComparer.Ordinal)];

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

    public WorkerIdentity? Find(string username) => _workers.GetValueOrDefault(username);
}