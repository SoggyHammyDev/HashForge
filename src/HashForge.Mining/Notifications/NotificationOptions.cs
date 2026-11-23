namespace HashForge.Mining.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>A Discord webhook URL. Leave empty to turn Discord notifications off.</summary>
    public string? DiscordWebhookUrl { get; set; }

    public bool NotifyBlockFound { get; set; } = true;

    public bool NotifyBestShare { get; set; } = true;

    /// <summary>The shortest gap between two best-share messages for one pool, so a new pool does not flood the channel.</summary>
    public double BestShareCooldownSeconds { get; set; } = 60;

    /// <summary>The name shown as the sender of each message.</summary>
    public string SenderName { get; set; } = "HashForge";
}
