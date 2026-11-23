using System.Text.Json.Serialization;

namespace HashForge.Bitcoin.Rpc;

public sealed record BlockTemplateTransaction(
    [property: JsonPropertyName("data")] string Data,
    [property: JsonPropertyName("txid")] string TxId);
