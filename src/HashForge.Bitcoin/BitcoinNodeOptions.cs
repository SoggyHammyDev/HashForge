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

    /// <summary>
    /// Where the node publishes new-block announcements over ZeroMQ (its -zmqpubhashblock address),
    /// for example tcp://bitcoind:28332. Leave empty to rely on polling alone.
    /// </summary>
    [RegularExpression("^$|^(tcp|ipc|inproc)://.+", ErrorMessage = "ZmqBlockUrl must be empty or a ZeroMQ address such as tcp://host:28332.")]
    public string ZmqBlockUrl { get; set; } = string.Empty;
}
