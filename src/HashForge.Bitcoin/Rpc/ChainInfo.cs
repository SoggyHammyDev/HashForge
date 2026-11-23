using System.Text.Json.Serialization;

namespace HashForge.Bitcoin.Rpc;

public sealed record ChainInfo
{
    [JsonPropertyName("chain")]
    public string Chain { get; init; } = string.Empty;

    [JsonPropertyName("blocks")]
    public int Blocks { get; init; }

    [JsonPropertyName("headers")]
    public int Headers { get; init; }

    [JsonPropertyName("bestblockhash")]
    public string BestBlockHash { get; init; } = string.Empty;

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; init; }

    [JsonPropertyName("verificationprogress")]
    public double VerificationProgress { get; init; }

    [JsonPropertyName("initialblockdownload")]
    public bool InitialBlockDownload { get; init; }

    [JsonPropertyName("pruned")]
    public bool Pruned { get; init; }

    [JsonPropertyName("size_on_disk")]
    public long SizeOnDisk { get; init; }
}
