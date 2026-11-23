namespace HashForge.Bitcoin.Rpc;

public interface IBlockSubmitter
{
    Task<BlockSubmissionResult> SubmitBlockAsync(string blockHex, CancellationToken cancellationToken);
}
