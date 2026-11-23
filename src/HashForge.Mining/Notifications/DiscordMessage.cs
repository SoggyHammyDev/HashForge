using System.Text.Json.Serialization;

namespace HashForge.Mining.Notifications;

public sealed record DiscordMessage(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("embeds")] IReadOnlyList<DiscordEmbed> Embeds);
