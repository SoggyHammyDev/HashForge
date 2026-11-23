using System.Text.Json;
using HashForge.Mining.State;

namespace HashForge.Mining.Payouts;

/// <summary>Saves and restores every pool's share history so payout splits survive a restart.</summary>
public sealed class PayoutState(IPayoutSchemeRegistry schemes) : IStatePersistable
{
    public string StateKey => "payouts";

    public string ExportState() => JsonSerializer.Serialize(schemes.ExportHistory());

    public void ImportState(string state) =>
        schemes.ImportHistory(JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<ShareBucket>>>(state) ?? []);
}