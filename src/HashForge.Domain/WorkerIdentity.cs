using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace HashForge.Domain;

/// <summary>A miner's payout address plus the name of one of their machines.</summary>
public sealed partial record WorkerIdentity(string Address, string WorkerName)
{
    public const string DefaultWorkerName = "default";

    private const char Separator = '.';

    public string FullName => $"{Address}{Separator}{WorkerName}";

    /// <summary>
    /// Parses a Stratum username of the form <c>address</c> or <c>address.worker</c>. Both parts come from
    /// the miner, so anything not shaped like an address, or a worker name with unusual characters, is refused.
    /// </summary>
    public static bool TryParse(string? username, [NotNullWhen(true)] out WorkerIdentity? identity)
    {
        identity = null;
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var parts = username.Trim().Split(Separator, 2, StringSplitOptions.TrimEntries);
        var workerName = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : DefaultWorkerName;
        if (!AddressFormat.IsPlausible(parts[0]) || !WorkerNamePattern().IsMatch(workerName))
        {
            return false;
        }

        identity = new WorkerIdentity(parts[0], workerName);
        return true;
    }

    public override string ToString() => FullName;

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,32}$")]
    private static partial Regex WorkerNamePattern();
}
