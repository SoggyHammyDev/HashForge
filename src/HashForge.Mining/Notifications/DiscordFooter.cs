using System.Text.Json.Serialization;

namespace HashForge.Mining.Notifications;

public sealed record DiscordFooter([property: JsonPropertyName("text")] string Text);
