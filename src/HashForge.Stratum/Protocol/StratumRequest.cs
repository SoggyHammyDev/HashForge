using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace HashForge.Stratum.Protocol;

/// <summary>A JSON-RPC request from a miner.</summary>
public sealed record StratumRequest(JsonElement? Id, string Method, IReadOnlyList<JsonElement> Parameters)
{
    public static bool TryParse(string line, [NotNullWhen(true)] out StratumRequest? request)
    {
        request = null;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("method", out var method)
                || method.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            JsonElement? id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : null;
            var parameters = root.TryGetProperty("params", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Array
                ? paramsElement.EnumerateArray().Select(p => p.Clone()).ToList()
                : [];
            request = new StratumRequest(id, method.GetString()!, parameters);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The string parameter at <paramref name="index"/>, or null when it is absent or not a string.</summary>
    public string? GetString(int index) =>
        index < Parameters.Count && Parameters[index].ValueKind == JsonValueKind.String ? Parameters[index].GetString() : null;

    public double? GetNumber(int index) =>
        index < Parameters.Count && Parameters[index].ValueKind == JsonValueKind.Number ? Parameters[index].GetDouble() : null;
}
