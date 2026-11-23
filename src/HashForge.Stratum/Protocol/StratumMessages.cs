using System.Text.Json;
using System.Text.Json.Serialization;

namespace HashForge.Stratum.Protocol;

/// <summary>Builds and serialises the messages the pool sends to miners.</summary>
public static class StratumMessages
{
    public static string Response(JsonElement? id, object? result) => Serialize(new ResponseMessage(id, result, null));

    public static string Error(JsonElement? id, StratumError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Serialize(new ResponseMessage(id, null, error.ToWire()));
    }

    public static string Notification(string method, params object?[] parameters) =>
        Serialize(new NotificationMessage(null, method, parameters));

    private static string Serialize<T>(T message) => JsonSerializer.Serialize(message);

    private sealed record ResponseMessage(
        [property: JsonPropertyName("id")] JsonElement? Id,
        [property: JsonPropertyName("result")] object? Result,
        [property: JsonPropertyName("error")] object?[]? Error);

    private sealed record NotificationMessage(
        [property: JsonPropertyName("id")] object? Id,
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] object?[] Params);
}
