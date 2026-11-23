using Microsoft.Extensions.Options;

namespace HashForge.Mining.Notifications;

public sealed class NotificationOptionsValidator : IValidateOptions<NotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.DiscordWebhookUrl)
            && !(Uri.TryCreate(options.DiscordWebhookUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            errors.Add("DiscordWebhookUrl must be an https:// URL, or empty to disable Discord notifications.");
        }

        if (options.BestShareCooldownSeconds < 0)
        {
            errors.Add("BestShareCooldownSeconds must not be negative.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
