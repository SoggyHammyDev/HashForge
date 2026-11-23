using System.Globalization;

namespace HashForge.Domain.Formatting;

/// <summary>Turns raw numbers into text people can read at a glance.</summary>
public static class Display
{
    public const string None = "–";
    public const long SatoshisPerBitcoin = 100_000_000;

    private const double UnitStep = 1000;
    private const int ShortPrefixLength = 10;
    private const int ShortSuffixLength = 6;

    private static readonly string[] HashrateUnits = ["H/s", "kH/s", "MH/s", "GH/s", "TH/s", "PH/s", "EH/s", "ZH/s"];
    private static readonly string[] DifficultyUnits = ["", "K", "M", "G", "T", "P", "E", "Z"];
    private static readonly string[] ByteUnits = ["B", "kB", "MB", "GB", "TB"];

    public static string Hashrate(double hashesPerSecond) => Scale(hashesPerSecond, HashrateUnits);

    public static string Difficulty(double difficulty) => Scale(difficulty, DifficultyUnits);

    public static string Bytes(long bytes) => Scale(bytes, ByteUnits);

    public static string Percent(double percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Bitcoin(long satoshis) =>
        ((decimal)satoshis / SatoshisPerBitcoin).ToString("0.00000000", CultureInfo.InvariantCulture) + " BTC";

    /// <summary>How long ago something happened, e.g. "5m ago".</summary>
    public static string Age(DateTimeOffset? timestamp, DateTimeOffset now)
    {
        if (timestamp is not { } then)
        {
            return "never";
        }

        var elapsed = now - then;
        return elapsed switch
        {
            _ when elapsed < TimeSpan.FromMinutes(1) => $"{Math.Max((int)elapsed.TotalSeconds, 0)}s ago",
            _ when elapsed < TimeSpan.FromHours(1) => $"{(int)elapsed.TotalMinutes}m ago",
            _ when elapsed < TimeSpan.FromDays(1) => $"{(int)elapsed.TotalHours}h ago",
            _ => $"{(int)elapsed.TotalDays}d ago",
        };
    }

    /// <summary>Shortens a long address or hash to its start and end.</summary>
    public static string Shorten(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= ShortPrefixLength + ShortSuffixLength + 1
            ? value
            : $"{value[..ShortPrefixLength]}…{value[^ShortSuffixLength..]}";
    }

    private static string Scale(double value, string[] units)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            return $"0 {units[0]}".TrimEnd();
        }

        var unit = Math.Min((int)Math.Floor(Math.Log(value, UnitStep)), units.Length - 1);
        unit = Math.Max(unit, 0);
        var scaled = value / Math.Pow(UnitStep, unit);
        return $"{scaled.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}".TrimEnd();
    }
}
