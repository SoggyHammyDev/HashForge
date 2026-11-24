using System.Text.Json.Serialization;

namespace HashForge.Bitcoin.Rpc;

/// <summary>Where the node places one block relative to its best chain.</summary>
public sealed record BlockHeaderInfo
{
    /// <summary>How many blocks deep it is on the best chain, or -1 when it is not on the best chain.</summary>
    [JsonPropertyName("confirmations")]
    public int Confirmations { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    public bool IsOnBestChain => Confirmations >= 0;
}
