using HashForge.Domain;

namespace HashForge.Mining;

public sealed class MiningOptions
{
    public const string SectionName = "Mining";
    public const int MaxCoinbaseTagLength = 40;

    /// <summary>Pools started at launch. More can be added at runtime from the admin page.</summary>
    public IList<PoolDefinition> Pools { get; init; } = [];

    /// <summary>Text written into the coinbase of every block the pool finds.</summary>
    public string CoinbaseTag { get; set; } = "/HashForge/";

    /// <summary>
    /// The most payout outputs any pool may put in a block, whatever the pool's own limit says; 0 means no
    /// ceiling. Every output takes block space the node must have kept free (-blockreservedweight), so set
    /// this when the pool uses a node whose reserved space cannot be raised: a block that outgrows it is
    /// rejected and its reward lost. Bitcoin Core's default reservation is small; ten outputs fit comfortably.
    /// </summary>
    public int MaxCoinbaseOutputsCeiling { get; set; }

    public int Extranonce2Size { get; set; } = 8;

    /// <summary>How often the node is asked whether a new block has arrived.</summary>
    public double ChainTipPollSeconds { get; set; } = 1;

    /// <summary>How often work is rebuilt between blocks, to pick up new transactions and payout changes.</summary>
    public double TemplateRefreshSeconds { get; set; } = 30;

    public double NetworkStatsRefreshSeconds { get; set; } = 30;

    public double HashrateWindowMinutes { get; set; } = 10;

    public double StateSnapshotSeconds { get; set; } = 10;

    /// <summary>How often pool and miner hashrates are recorded for the charts.</summary>
    public double HistorySampleMinutes { get; set; } = 5;

    /// <summary>How far back each miner's hashrate chart goes.</summary>
    public double MinerHistoryHours { get; set; } = 24;
}
