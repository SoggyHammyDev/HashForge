using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using HashForge.Bitcoin.Primitives;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;

namespace HashForge.Mining.Jobs;

/// <summary>One unit of work handed to miners: a block template plus the coinbase that pays this pool's miners.</summary>
public sealed class MiningJob
{
    private readonly ConcurrentDictionary<string, byte> _submissions = new(StringComparer.OrdinalIgnoreCase);

    public MiningJob(
        string id,
        PoolDefinition pool,
        BlockTemplate template,
        CoinbaseTransaction coinbase,
        IReadOnlyList<PayoutEntry> payouts,
        int? minersPaid = null,
        long templateSequence = 0)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(payouts);

        Id = id;
        Pool = pool;
        Template = template;
        Coinbase = coinbase;
        Payouts = payouts;
        MinersPaid = minersPaid ?? payouts.Count;
        TemplateSequence = templateSequence;
        Bits = uint.Parse(template.Bits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        NetworkTarget = DifficultyMath.TargetFromCompact(Bits);
        PreviousBlockHash = HashHex.FromDisplayHex(template.PreviousBlockHash);
        MerkleBranch = HashForge.Bitcoin.Primitives.MerkleBranch.Build([.. template.Transactions.Select(t => HashHex.FromDisplayHex(t.TxId))]);
    }

    public string Id { get; }

    public PoolDefinition Pool { get; }

    public BlockTemplate Template { get; }

    /// <summary>
    /// Which template this job was built from, counting up with each one received from the node.
    /// A higher number is always newer work.
    /// </summary>
    public long TemplateSequence { get; }

    public CoinbaseTransaction Coinbase { get; }

    public IReadOnlyList<PayoutEntry> Payouts { get; }

    /// <summary>How many of the payouts go to miners, as opposed to the pool's fee output.</summary>
    public int MinersPaid { get; }

    public uint Bits { get; }

    public BigInteger NetworkTarget { get; }

    /// <summary>The previous block hash in internal byte order.</summary>
    public byte[] PreviousBlockHash { get; }

    public IReadOnlyList<byte[]> MerkleBranch { get; }

    /// <summary>Records a submission and returns false when the same one was already seen.</summary>
    public bool TryRegisterSubmission(string submissionKey) => _submissions.TryAdd(submissionKey, 0);
}
