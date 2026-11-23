using System.Text.Json.Serialization;

namespace HashForge.Mining.Notifications;

public sealed record DiscordEmbed(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("color")] int Color,
    [property: JsonPropertyName("fields")] IReadOnlyList<DiscordField> Fields,
    [property: JsonPropertyName("footer")] DiscordFooter Footer,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
