using HashForge.Bitcoin.Primitives;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Jobs;
using HashForge.Mining.Shares;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.UnitTests;

/// <summary>Builders for the objects most tests need.</summary>
internal static class TestData
{
    public const string MinerAddress = "miner-address";
    public const string FeeAddress = "pool-fee-address";
    public const string MainnetBits = "1d00ffff";
    public const string RegtestBits = "207fffff";
    public const string PreviousBlockHash = "00000000000000000001aa0000000000000000000000000000000000000000ff";

    public static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly WorkerIdentity Worker = new(MinerAddress, "rig1");
    public static readonly byte[] Extranonce1 = [0xaa, 0xbb, 0xcc, 0xdd];

    public static PoolDefinition Pool(string name = "pplns", PayoutScheme scheme = PayoutScheme.Pplns, int port = 3334) => new()
    {
        Name = name,
        Scheme = scheme,
        Port = port,
        FeeAddress = FeeAddress,
        FeePercent = 1m,
    };

    public static BlockTemplate Template(
        string bits = MainnetBits,
        string previousBlockHash = PreviousBlockHash,
        int transactionCount = 0,
        string? witnessCommitment = null) => new()
        {
            Version = 0x20000000,
            PreviousBlockHash = previousBlockHash,
            Bits = bits,
            CoinbaseValue = 312_500_000,
            CurrentTime = 1_700_000_000,
            MinimumTime = 1_699_990_000,
            Height = 850_000,
            DefaultWitnessCommitment = witnessCommitment,
            Transactions =
            [
                .. Enumerable.Range(1, transactionCount).Select(i =>
                    new BlockTemplateTransaction($"0{i}ff", HashHex.ToDisplayHex(Sha256d.Hash([(byte)i])))),
            ],
        };

    public static IOptions<MiningOptions> Options(Action<MiningOptions>? configure = null)
    {
        var options = new MiningOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    public static MiningJob Job(PoolDefinition? pool = null, BlockTemplate? template = null, string id = "1")
    {
        template ??= Template();
        var coinbase = CoinbaseBuilder.Build(new CoinbaseSpec(
            template.Height,
            "/test/"u8.ToArray(),
            ExtranonceSize: 12,
            [new TransactionOutput(template.CoinbaseValue, [0x51])],
            template.DefaultWitnessCommitment is null ? null : Convert.FromHexString(template.DefaultWitnessCommitment)));
        return new MiningJob(id, pool ?? Pool(), template, coinbase, [new PayoutEntry(MinerAddress, template.CoinbaseValue)]);
    }

    public static ShareResult Share(
        string poolName = "pplns",
        WorkerIdentity? worker = null,
        ShareOutcome outcome = ShareOutcome.Accepted,
        double credited = 100,
        double actual = 150,
        DateTimeOffset? timestamp = null) =>
        new(poolName, worker ?? Worker, outcome, outcome == ShareOutcome.Accepted ? credited : 0, actual, timestamp ?? Now);

    public static ShareSubmission Submission(
        MiningJob job,
        uint nonce = 0,
        double difficulty = 1e-7,
        double? previousDifficulty = null,
        string extranonce2Hex = "0000000000000001",
        string? timeHex = null,
        string? versionBitsHex = null,
        uint versionMask = 0,
        string? poolName = null) =>
        new(
            poolName ?? job.Pool.Name,
            Worker,
            job.Id,
            Extranonce1,
            extranonce2Hex,
            timeHex ?? job.Template.CurrentTime.ToString("x8"),
            nonce.ToString("x8"),
            versionBitsHex,
            versionMask,
            difficulty,
            previousDifficulty);

    public static FoundBlock Block(string poolName = "pplns", PayoutScheme scheme = PayoutScheme.Pplns, WorkerIdentity? worker = null, bool accepted = true) =>
        new(poolName, scheme, 850_000, "00ab", worker ?? Worker, 312_500_000, [new PayoutEntry(MinerAddress, 312_500_000)], 42, Now, accepted, accepted ? null : "high-hash") { MinersPaid = 1 };
}
