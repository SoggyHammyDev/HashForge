using System.Text.Json.Serialization;

namespace HashForge.Mining.History;

/// <summary>One reading of a miner's hashrate, kept so the UI can chart it over time.</summary>
/// <remarks>Stored compactly because every active miner keeps a day of these in the pool's saved state.</remarks>
public sealed record MinerHistorySample(
    [property: JsonPropertyName("t")] long UnixSeconds,
    [property: JsonPropertyName("h")] double Hashrate,
    [property: JsonPropertyName("w")] int Workers)
{
    [JsonIgnore]
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeSeconds(UnixSeconds);
}