using System.Text.Json.Serialization;

namespace HashForge.Mining.Notifications;

public sealed record DiscordField(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("inline")] bool Inline = true);
