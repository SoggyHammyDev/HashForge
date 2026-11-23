using System.Collections.Concurrent;
using System.Globalization;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Payouts;

namespace HashForge.Mining.Jobs;

/// <summary>Holds the current block template and the jobs built from it.</summary>
public sealed class JobManager(IJobFactory factory, IPayoutPlanSource planSource) : IJobSource, ITemplateSink
{
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<string, MiningJob> _jobsById = new(StringComparer.OrdinalIgnoreCase);
    private volatile TemplateState? _state;
    private long _lastSequence;
    private long _lastJobId;

    public event EventHandler<JobsChangedEventArgs>? JobsChanged;

    public void Publish(BlockTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        bool isNewBlock;
        lock (_gate)
        {
            isNewBlock = _state?.Template.PreviousBlockHash != template.PreviousBlockHash;
            if (isNewBlock)
            {
                _jobsById.Clear();
            }

            // Templates are numbered in the order they arrive, so every job can be compared for freshness.
            _state = new TemplateState(template, ++_lastSequence);
        }

        JobsChanged?.Invoke(this, new JobsChangedEventArgs(isNewBlock));
    }

    public async Task<MiningJob?> GetCurrentJobAsync(PoolDefinition pool, string minerAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);

        if (_state is not { } state)
        {
            return null;
        }

        // When the payout depends on the miner (solo, or a shared pool with no history yet) each address
        // needs its own coinbase; otherwise every miner on the pool shares one job.
        var key = planSource.IsMinerSpecific(pool) ? $"{pool.Name}|{minerAddress}" : pool.Name;
        var job = state.Jobs.GetOrAdd(key, _ => new Lazy<Task<MiningJob>>(() => CreateAsync(pool, state, minerAddress)));
        try
        {
            return await job.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (job.Value.IsFaulted)
        {
            // A failed build is forgotten so the next request tries again instead of failing until the next template.
            state.Jobs.TryRemove(new KeyValuePair<string, Lazy<Task<MiningJob>>>(key, job));
            throw;
        }
    }

    /// <summary>
    /// Only jobs built on the current chain tip are returned. The tip is checked here, at the moment of use,
    /// so a job that finished building just after a new block arrived can never be treated as valid.
    /// </summary>
    public MiningJob? Find(string jobId) =>
        _jobsById.GetValueOrDefault(jobId) is { } job && IsOnCurrentTip(job) ? job : null;

    private bool IsOnCurrentTip(MiningJob job) =>
        job.Template.PreviousBlockHash == _state?.Template.PreviousBlockHash;

    private async Task<MiningJob> CreateAsync(PoolDefinition pool, TemplateState state, string minerAddress)
    {
        var identity = new JobIdentity(Interlocked.Increment(ref _lastJobId).ToString("x", CultureInfo.InvariantCulture), state.Sequence);
        var job = await factory.CreateAsync(identity, pool, state.Template, minerAddress, CancellationToken.None).ConfigureAwait(false);
        _jobsById[identity.Id] = job;
        if (!IsOnCurrentTip(job))
        {
            // The chain moved on while this job was being built; do not keep it around.
            _jobsById.TryRemove(identity.Id, out _);
        }

        return job;
    }

    private sealed record TemplateState(BlockTemplate Template, long Sequence)
    {
        public ConcurrentDictionary<string, Lazy<Task<MiningJob>>> Jobs { get; } = new(StringComparer.Ordinal);
    }
}