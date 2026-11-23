using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Notifications;

/// <summary>Posts notifications to a Discord webhook. Does nothing when no webhook is configured.</summary>
public sealed class DiscordWebhookChannel(IHttpClientFactory httpClientFactory, IOptions<NotificationOptions> options) : INotificationChannel
{
    public const string HttpClientName = "DiscordWebhook";

    public async Task SendAsync(PoolNotification notification, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl))
        {
            return;
        }

        var message = DiscordMessageFactory.Create(notification, settings.SenderName);
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client
            .PostAsJsonAsync(new Uri(settings.DiscordWebhookUrl), message, cancellationToken)
            .ConfigureAwait(false);

        // The URL is a secret, so failures report only the status code.
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Discord answered {(int)response.StatusCode}.", null, response.StatusCode);
        }
    }
}
