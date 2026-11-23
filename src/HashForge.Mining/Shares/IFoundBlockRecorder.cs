using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Jobs;

namespace HashForge.Mining.Shares;

public interface IFoundBlockRecorder
{
    /// <summary>Archives the block and, when the node accepted it, announces it and requests fresh work.</summary>
    Task RecordAsync(MiningJob job, BlockCandidate block, ShareResult share, BlockSubmissionResult nodeVerdict, CancellationToken cancellationToken);
}