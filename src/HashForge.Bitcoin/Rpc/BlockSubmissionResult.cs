namespace HashForge.Bitcoin.Rpc;

/// <summary>The node's answer to submitblock; a null reason means the block was accepted.</summary>
public sealed record BlockSubmissionResult(string? RejectReason)
{
    public bool Accepted => RejectReason is null;
}
