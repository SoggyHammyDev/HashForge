using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace HashForge.Stratum;

public sealed class StratumOptions : IValidatableObject
{
    public const string SectionName = "Stratum";

    /// <summary>The difficulty a miner starts on before vardiff has measured it.</summary>
    [Range(double.Epsilon, double.MaxValue)]
    public double InitialDifficulty { get; set; } = 1024;

    [Range(double.Epsilon, double.MaxValue)]
    public double MinDifficulty { get; set; } = 0.001;

    [Range(double.Epsilon, double.MaxValue)]
    public double MaxDifficulty { get; set; } = 1_000_000_000_000;

    /// <summary>How many shares per minute vardiff steers each connection towards.</summary>
    [Range(0.1, 600)]
    public double TargetSharesPerMinute { get; set; } = 12;

    [Range(5, 3600)]
    public double RetargetSeconds { get; set; } = 60;

    /// <summary>How far the share rate may drift from the target before difficulty is changed.</summary>
    [Range(1, 99)]
    public double VariancePercent { get; set; } = 30;

    [Range(10, 86400)]
    public double IdleTimeoutSeconds { get; set; } = 600;

    /// <summary>How long a miner may take to accept a message before the connection is dropped.</summary>
    [Range(1, 600)]
    public double SendTimeoutSeconds { get; set; } = 30;

    /// <summary>The most miners that may be connected at once, across all pools.</summary>
    [Range(1, 1_000_000)]
    public int MaxConnections { get; set; } = 10_000;

    /// <summary>
    /// The most connections one IP address may hold; 0 means no limit. Leave at 0 where every miner
    /// appears to come from one address, such as behind Docker Desktop or a proxy.
    /// </summary>
    [Range(0, 1_000_000)]
    public int MaxConnectionsPerAddress { get; set; }

    [Range(256, 1_048_576)]
    public int MaxLineLength { get; set; } = 16_384;

    /// <summary>The block-version bits miners may roll (BIP 320), as hex.</summary>
    [RegularExpression("^[0-9a-fA-F]{8}$")]
    public string VersionRollingMask { get; set; } = "1fffe000";

    public uint VersionRollingMaskValue => uint.Parse(VersionRollingMask, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinDifficulty > MaxDifficulty)
        {
            yield return new ValidationResult("MinDifficulty must not exceed MaxDifficulty.", [nameof(MinDifficulty)]);
        }

        if (InitialDifficulty < MinDifficulty || InitialDifficulty > MaxDifficulty)
        {
            yield return new ValidationResult("InitialDifficulty must lie between MinDifficulty and MaxDifficulty.", [nameof(InitialDifficulty)]);
        }
    }
}
