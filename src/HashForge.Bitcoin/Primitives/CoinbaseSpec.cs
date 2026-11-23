namespace HashForge.Bitcoin.Primitives;

/// <summary>Everything needed to build a coinbase transaction.</summary>
/// <param name="WitnessCommitmentScript">The node's default witness commitment script, when the block has one.</param>
public sealed record CoinbaseSpec(
    int Height,
    byte[] Tag,
    int ExtranonceSize,
    IReadOnlyList<TransactionOutput> Outputs,
    byte[]? WitnessCommitmentScript);
