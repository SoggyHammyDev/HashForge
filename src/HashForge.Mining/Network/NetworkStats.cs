namespace HashForge.Mining.Network;

/// <summary>A point-in-time view of the Bitcoin network and of the node's sync state.</summary>
public sealed record NetworkStats(
    string Chain,
    int Height,
    int Headers,
    double Difficulty,
    double NetworkHashrate,
    string BestBlockHash,
    bool InitialBlockDownload,
    double VerificationProgress,
    bool Pruned,
    long SizeOnDisk,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The chain name Bitcoin Core reports for a local regression-test network.</summary>
    public const string RegtestChain = "regtest";

    /// <summary>When the newest block on the network was mined, if the node reported it.</summary>
    public DateTimeOffset? LastBlockAt { get; init; }

    public bool IsSynced => !InitialBlockDownload && Height >= Headers;

    /// <summary>How far through verifying the chain the node is, from 0 to 100.</summary>
    public double SyncPercent => VerificationProgress * 100;

    /// <summary>
    /// False while the node is still downloading the chain, when any work would build on an old block.
    /// A regtest chain is exempt: it reports "downloading" whenever its newest block is more than a day old.
    /// </summary>
    public bool IsReadyForMining => !InitialBlockDownload || Chain == RegtestChain;
}
