using System.Net;

namespace HashForge.Stratum.Transport;

/// <summary>A line-based connection to one miner.</summary>
public interface IStratumConnection : IAsyncDisposable
{
    IPAddress RemoteAddress { get; }

    /// <summary>Reads the next line, or returns null when the miner has disconnected.</summary>
    Task<string?> ReadLineAsync(CancellationToken cancellationToken);

    Task WriteLineAsync(string line, CancellationToken cancellationToken);
}
