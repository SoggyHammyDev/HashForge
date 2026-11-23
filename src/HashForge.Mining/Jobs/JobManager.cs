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
    private ConcurrentDictionary<string, Lazy<Task<MiningJob>>> _currentJobs = new(StringComparer.Ordinal);
    private BlockTemplate? _template;
    private long _lastJobId;

    public event EventHandler<JobsChangedEventArgs>? JobsChanged;

    public void Publish(BlockTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        bool isNewBlock;
        lock (_gate)
        {
            isNewBlock = _template?.PreviousBlockHash != template.PreviousBlockHash;
            if (isNewBlock)
            {
                _jobsById.Clear();
            }

            _template = template;
            _currentJobs = new ConcurrentDictionary<string, Lazy<Task<MiningJob>>>(StringComparer.Ordinal);
        }

        JobsChanged?.Invoke(this, new JobsChangedEventArgs(isNewBlock));
    }

    public async Task<MiningJob?> GetCurrentJobAsync(PoolDefinition pool, string minerAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);

        BlockTemplate? template;
        ConcurrentDictionary<string, Lazy<Task<MiningJob>>> jobs;
        lock (_gate)
        {
            template = _template;
            jobs = _currentJobs;
        }

        if (template is null)
        {
            return null;
        }

        // When the payout depends on the miner (solo, or a shared pool with no history yet) each address
        // needs its own coinbase; otherwise every miner on the pool shares one job.
        var key = planSource.IsMinerSpecific(pool) ? $"{pool.Name}|{minerAddress}" : pool.Name;
        var job = jobs.GetOrAdd(key, _ => new Lazy<Task<MiningJob>>(() => CreateAsync(pool, template, minerAddress)));
        try
        {
            return await job.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (job.Value.IsFaulted)
        {
            // A failed build is forgotten so the next request tries again instead of failing until the next template.
            jobs.TryRemove(new KeyValuePair<string, Lazy<Task<MiningJob>>>(key, job));
            throw;
        }
    }

    public MiningJob? Find(string jobId) => _jobsById.GetValueOrDefault(jobId);

    private async Task<MiningJob> CreateAsync(PoolDefinition pool, BlockTemplate template, string minerAddress)
    {
        var jobId = Interlocked.Increment(ref _lastJobId).ToString("x", CultureInfo.InvariantCulture);
        var job = await factory.CreateAsync(jobId, pool, template, minerAddress, CancellationToken.None).ConfigureAwait(false);
        _jobsById[jobId] = job;
        return job;
    }
}
