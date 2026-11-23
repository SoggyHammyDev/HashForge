using System.Globalization;
using HashForge.Bitcoin.Primitives;
using HashForge.Mining.Jobs;

namespace HashForge.Stratum.Protocol;

/// <summary>Formats the work-related notifications of the Stratum protocol.</summary>
public static class JobNotification
{
    public const string NotifyMethod = "mining.notify";
    public const string SetDifficultyMethod = "mining.set_difficulty";

    private const string UInt32HexFormat = "x8";

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

    public static string SetDifficulty(double difficulty) => StratumMessages.Notification(SetDifficultyMethod, difficulty);
}
