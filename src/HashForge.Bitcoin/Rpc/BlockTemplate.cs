using System.Text.Json.Serialization;

namespace HashForge.Bitcoin.Rpc;

/// <summary>The subset of Bitcoin Core's getblocktemplate result that the pool uses.</summary>
public sealed record BlockTemplate
{
    [JsonPropertyName("version")]
    public required uint Version { get; init; }

    [JsonPropertyName("previousblockhash")]
    public required string PreviousBlockHash { get; init; }

    [JsonPropertyName("transactions")]
    public IReadOnlyList<BlockTemplateTransaction> Transactions { get; init; } = [];

    [JsonPropertyName("coinbasevalue")]
    public required long CoinbaseValue { get; init; }

    [JsonPropertyName("bits")]
    public required string Bits { get; init; }

    [JsonPropertyName("curtime")]
    public required uint CurrentTime { get; init; }

    [JsonPropertyName("mintime")]
    public uint MinimumTime { get; init; }

    [JsonPropertyName("height")]
    public required int Height { get; init; }

    [JsonPropertyName("default_witness_commitment")]
    public string? DefaultWitnessCommitment { get; init; }
}
