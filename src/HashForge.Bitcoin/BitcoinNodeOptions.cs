using System.ComponentModel.DataAnnotations;

namespace HashForge.Bitcoin;

public sealed class BitcoinNodeOptions
{
    public const string SectionName = "BitcoinNode";

    [Required]
    public Uri? RpcUrl { get; set; }

    [Required]
    public string RpcUser { get; set; } = string.Empty;

    [Required]
    public string RpcPassword { get; set; } = string.Empty;

    [Range(1, 600)]
    public int RpcTimeoutSeconds { get; set; } = 30;
}
