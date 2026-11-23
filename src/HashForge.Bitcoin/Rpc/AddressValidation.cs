using System.Text.Json.Serialization;

namespace HashForge.Bitcoin.Rpc;

public sealed record AddressValidation
{
    [JsonPropertyName("isvalid")]
    public bool IsValid { get; init; }

    [JsonPropertyName("scriptPubKey")]
    public string? ScriptPubKey { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
