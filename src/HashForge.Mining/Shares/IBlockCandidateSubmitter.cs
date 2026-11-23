using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Jobs;

namespace HashForge.Mining.Shares;

public interface IBlockCandidateSubmitter
{
    /// <summary>Hands a solved block to the node. Never cancelled, and retried, because a block is too valuable to drop.</summary>
    Task<BlockSubmissionResult> SubmitAsync(MiningJob job, BlockCandidate block);
}