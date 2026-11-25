using System.ComponentModel.DataAnnotations;

namespace HashForge.Web;

public sealed class WebOptions
{
    public const string SectionName = "Web";

    /// <summary>The first port Docker publishes for pools started from the admin page.</summary>
    public const int FirstSparePoolPort = 3337;

    /// <summary>How often live pages re-read the statistics.</summary>
    [Range(1, 3600)]
    public double RefreshSeconds { get; set; } = 5;

    /// <summary>The host name miners should connect to, shown on the "connect" page.</summary>
    public string StratumHost { get; set; } = "your-pool-host";

    [Range(1, 500)]
    public int RecentBlockCount { get; set; } = 25;

    [Range(1, 720)]
    public double ChartHours { get; set; } = 24;

    /// <summary>
    /// IP addresses of reverse proxies in front of the pool. Their X-Forwarded-For header is trusted, so the
    /// admin allow-list sees the visitor's real address. Leave empty when clients connect directly.
    /// </summary>
    public IList<string> TrustedProxies { get; init; } = [];

    /// <summary>
    /// The request header in which a reverse proxy names the sub-path it serves the pool under, for example
    /// <c>X-Ingress-Path</c> for Home Assistant or <c>X-Forwarded-Prefix</c>. Leave empty when the pool is
    /// served from the root of its address.
    /// </summary>
    public string PathBaseHeader { get; set; } = string.Empty;

    /// <summary>The proxies actually configured; blank entries (an unset environment variable) are ignored.</summary>
    public IEnumerable<string> ConfiguredProxies => TrustedProxies.Where(proxy => !string.IsNullOrWhiteSpace(proxy));
}
