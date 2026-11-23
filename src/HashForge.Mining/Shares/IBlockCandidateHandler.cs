using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Jobs;

namespace HashForge.Mining.Shares;

public interface IBlockCandidateHandler
{
    /// <summary>Hands a solved block to the node. Never cancelled, and retried, because a block is too valuable to drop.</summary>
    Task<BlockSubmissionResult> SubmitAsync(MiningJob job, BlockCandidate block);

    /// <summary>Archives the block and, when the node accepted it, announces it.</summary>
    Task RecordAsync(MiningJob job, BlockCandidate block, ShareResult share, BlockSubmissionResult nodeVerdict, CancellationToken cancellationToken);
}
