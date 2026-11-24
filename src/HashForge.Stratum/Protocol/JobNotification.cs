using System.Globalization;
using HashForge.Bitcoin.Primitives;
using HashForge.Mining.Jobs;

using System.Runtime.CompilerServices;

namespace HashForge.Stratum.Protocol;

/// <summary>Formats the work-related notifications of the Stratum protocol.</summary>
public static class JobNotification
{
    public const string NotifyMethod = "mining.notify";
    public const string SetDifficultyMethod = "mining.set_difficulty";

    private const string UInt32HexFormat = "x8";

    private static readonly ConditionalWeakTable<MiningJob, EncodedNotification> Encoded = [];

    public static string Notify(MiningJob job, bool cleanJobs)
    {
        ArgumentNullException.ThrowIfNull(job);
        return StratumMessages.Notification(
            NotifyMethod,
            job.Id,
            HashHex.ToStratumPreviousHash(job.Template.PreviousBlockHash),
            Convert.ToHexStringLower(job.Coinbase.Prefix),
            Convert.ToHexStringLower(job.Coinbase.Suffix),
            job.MerkleBranch.Select(Convert.ToHexStringLower).ToArray(),
            job.Template.Version.ToString(UInt32HexFormat, CultureInfo.InvariantCulture),
            job.Template.Bits,
            job.Template.CurrentTime.ToString(UInt32HexFormat, CultureInfo.InvariantCulture),
            cleanJobs);
    }

    /// <summary>
    /// The same notification as an encoded line, built once per job and reused for every miner it is sent to.
    /// The cache holds on to nothing: an entry goes when its job does.
    /// </summary>
    public static ReadOnlyMemory<byte> NotifyLine(MiningJob job, bool cleanJobs)
    {
        ArgumentNullException.ThrowIfNull(job);
        return Encoded.GetValue(job, static j => new EncodedNotification(j)).Line(cleanJobs);
    }

    public static string SetDifficulty(double difficulty) => StratumMessages.Notification(SetDifficultyMethod, difficulty);
}
