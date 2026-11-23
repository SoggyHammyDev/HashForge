namespace HashForge.Domain;

/// <summary>Effort and hashrate calculations.</summary>
public static class WorkMath
{
    /// <summary>The average number of hashes needed to find one difficulty-1 share.</summary>
    public const double HashesPerDifficulty = 4294967296d;

    private const double PercentScale = 100d;

    /// <summary>Work submitted so far as a percentage of the work statistically required for a block.</summary>
    public static double EffortPercent(double accumulatedDifficulty, double networkDifficulty) =>
        networkDifficulty <= 0 ? 0 : accumulatedDifficulty / networkDifficulty * PercentScale;

    /// <summary>Estimated hashes per second from the share difficulty submitted over a period.</summary>
    public static double Hashrate(double difficultySum, TimeSpan period) =>
        period <= TimeSpan.Zero ? 0 : difficultySum * HashesPerDifficulty / period.TotalSeconds;
}
