using System.Globalization;
using System.Text.Json;
using HashForge.Stratum.Protocol;
using HashForge.Stratum.Sessions;
using Microsoft.Extensions.Options;

namespace HashForge.Stratum.Handlers;

/// <summary>mining.configure: negotiates optional extensions; only version rolling is supported.</summary>
public sealed class ConfigureHandler(IOptions<StratumOptions> options) : IStratumMethodHandler
{
    private const string VersionRolling = "version-rolling";
    private const string VersionRollingMask = "version-rolling.mask";

    public string Method => "mining.configure";

    public Task<StratumReply> HandleAsync(StratumSession session, StratumRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var extension in RequestedExtensions(request))
        {
            result[extension] = false;
        }

        if (result.ContainsKey(VersionRolling))
        {
            session.VersionMask = options.Value.VersionRollingMaskValue & RequestedMask(request);
            result[VersionRolling] = true;
            result[VersionRollingMask] = session.VersionMask.ToString("x8", CultureInfo.InvariantCulture);
        }

        return Task.FromResult(StratumReply.Ok(result));
    }

    private static IEnumerable<string> RequestedExtensions(StratumRequest request) =>
        request.Parameters.Count > 0 && request.Parameters[0].ValueKind == JsonValueKind.Array
            ? request.Parameters[0].EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)
            : [];

    /// <summary>The mask the miner asked for; a miner that names none may roll every bit the pool allows.</summary>
    private static uint RequestedMask(StratumRequest request) =>
        request.Parameters.Count > 1
        && request.Parameters[1].ValueKind == JsonValueKind.Object
        && request.Parameters[1].TryGetProperty(VersionRollingMask, out var mask)
        && mask.ValueKind == JsonValueKind.String
        && uint.TryParse(mask.GetString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : uint.MaxValue;
}
