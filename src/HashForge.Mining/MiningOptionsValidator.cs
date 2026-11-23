using HashForge.Mining.Pools;
using Microsoft.Extensions.Options;

namespace HashForge.Mining;

public sealed class MiningOptionsValidator : IValidateOptions<MiningOptions>
{
    private const int MinExtranonce2Size = 2;
    private const int MaxExtranonce2Size = 16;

    public ValidateOptionsResult Validate(string? name, MiningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Pools.SelectMany(PoolDefinitionValidator.Validate).ToList();
        errors.AddRange(FindDuplicates(options.Pools.Select(p => p.Name), "pool name"));
        errors.AddRange(FindDuplicates(options.Pools.Select(p => p.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)), "pool port"));

        if (System.Text.Encoding.UTF8.GetByteCount(options.CoinbaseTag) > MiningOptions.MaxCoinbaseTagLength)
        {
            errors.Add($"CoinbaseTag must be at most {MiningOptions.MaxCoinbaseTagLength} bytes.");
        }

        if (options.Extranonce2Size is < MinExtranonce2Size or > MaxExtranonce2Size)
        {
            errors.Add($"Extranonce2Size must be between {MinExtranonce2Size} and {MaxExtranonce2Size}.");
        }

        errors.AddRange(RequirePositive(
            (nameof(options.ChainTipPollSeconds), options.ChainTipPollSeconds),
            (nameof(options.TemplateRefreshSeconds), options.TemplateRefreshSeconds),
            (nameof(options.NetworkStatsRefreshSeconds), options.NetworkStatsRefreshSeconds),
            (nameof(options.HashrateWindowMinutes), options.HashrateWindowMinutes),
            (nameof(options.StateSnapshotSeconds), options.StateSnapshotSeconds),
            (nameof(options.HistorySampleMinutes), options.HistorySampleMinutes)));

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static IEnumerable<string> FindDuplicates(IEnumerable<string> values, string description) =>
        values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate {description} '{g.Key}'.");

    private static IEnumerable<string> RequirePositive(params (string Name, double Value)[] settings) =>
        settings.Where(s => s.Value <= 0).Select(s => $"{s.Name} must be greater than zero.");
}
