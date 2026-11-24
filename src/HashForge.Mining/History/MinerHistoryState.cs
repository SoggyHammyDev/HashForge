using System.Text.Json;
using HashForge.Mining.State;

namespace HashForge.Mining.History;

/// <summary>Saves and restores miners' hashrate history so their charts survive a restart.</summary>
public sealed class MinerHistoryState(MinerHistoryStore store) : IStatePersistable
{
    public string StateKey => "miner-history";

    public string ExportState() => JsonSerializer.Serialize(store.Export());

    public void ImportState(string state) =>
        store.Import(JsonSerializer.Deserialize<Dictionary<string, MinerHistorySample[]>>(state) ?? []);
}