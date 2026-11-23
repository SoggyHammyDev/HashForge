namespace HashForge.Stratum.Handlers;

/// <summary>Makes miner-supplied text safe to write to a log.</summary>
public static class LogText
{
    private const int MaxLength = 128;

    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var visible = new string([.. value.Take(MaxLength).Select(c => char.IsControl(c) ? '?' : c)]);
        return value.Length > MaxLength ? visible + "..." : visible;
    }
}
