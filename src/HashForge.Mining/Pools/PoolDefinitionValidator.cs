using System.Text.RegularExpressions;
using HashForge.Domain;

namespace HashForge.Mining.Pools;

/// <summary>Checks that a pool definition is internally consistent.</summary>
public static partial class PoolDefinitionValidator
{
    public const long DustLimitSatoshis = 546;
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    private const decimal MaxFeePercent = 100m;

    public static IReadOnlyList<string> Validate(PoolDefinition pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var errors = new List<string>();
        var label = $"Pool '{pool.Name}'";
        AddIf(errors, pool.Name is null || !NamePattern().IsMatch(pool.Name), "Pool name must be 1-32 lowercase letters, digits or hyphens.");
        AddIf(errors, pool.Port is < MinPort or > MaxPort, $"{label}: port must be between {MinPort} and {MaxPort}.");
        AddIf(errors, pool.FeePercent is < 0 or > MaxFeePercent, $"{label}: fee must be between 0 and {MaxFeePercent} percent.");
        AddIf(errors, string.IsNullOrWhiteSpace(pool.FeeAddress), $"{label}: a fee address is required.");
        AddIf(errors, pool.MaxCoinbaseOutputs < 1, $"{label}: at least one coinbase output is required.");
        AddIf(errors, pool.MinimumPayoutSatoshis < DustLimitSatoshis, $"{label}: minimum payout must be at least {DustLimitSatoshis} satoshis.");
        AddIf(errors, pool.PplnsWindowMultiplier <= 0, $"{label}: the PPLNS window multiplier must be greater than zero.");
        AddIf(
            errors,
            pool.Scheme == PayoutScheme.FinderBonus && pool.FinderBonusSatoshis < DustLimitSatoshis,
            $"{label}: the finder bonus must be at least {DustLimitSatoshis} satoshis.");
        return errors;
    }

    /// <summary>The first problem with the definition, or null when it is valid.</summary>
    public static string? FirstError(PoolDefinition pool) => Validate(pool) is [var first, ..] ? first : null;

    private static void AddIf(List<string> errors, bool condition, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }

    [GeneratedRegex("^[a-z0-9-]{1,32}$")]
    private static partial Regex NamePattern();
}
