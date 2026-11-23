using HashForge.Domain;
using HashForge.Domain.Formatting;

namespace HashForge.Mining.Notifications;

/// <summary>Lays notifications out as Discord embeds: a coloured card with a title and a grid of facts.</summary>
public static class DiscordMessageFactory
{
    public const int BlockFoundColor = 0xF7931A;
    public const int BestShareColor = 0x2A78D6;

    public static DiscordMessage Create(PoolNotification notification, string senderName)
    {
        var embed = notification switch
        {
            BlockFoundNotification found => BlockFound(found, senderName),
            BestShareNotification best => BestShare(best, senderName),
            _ => throw new ArgumentOutOfRangeException(nameof(notification), notification, "Unknown notification type."),
        };
        return new DiscordMessage(senderName, [embed]);
    }

    private static DiscordFooter Footer(string senderName) => new($"{senderName} mining pool");

    private static DiscordEmbed BlockFound(BlockFoundNotification notification, string senderName)
    {
        var block = notification.Block;
        return new DiscordEmbed(
            "🎉 Block found!",
            $"The **{block.PoolName}** pool mined block **{Display.Count(block.Height)}**.",
            BlockFoundColor,
            [
                new DiscordField("Reward", Display.Bitcoin(block.RewardSatoshis)),
                new DiscordField("Effort", Display.Percent(block.EffortPercent)),
                new DiscordField("Payout", $"{block.Scheme.Label()} · {Display.Count(block.MinersPaid)} miners paid"),
                new DiscordField("Found by", Code(block.Worker.FullName), Inline: false),
                new DiscordField("Block hash", Code(block.Hash), Inline: false),
            ],
            Footer(senderName),
            block.FoundAt);
    }

    private static DiscordEmbed BestShare(BestShareNotification notification, string senderName) => new(
        "💎 New best share",
        $"A new record for the **{notification.PoolName}** pool.",
        BestShareColor,
        [
            new DiscordField("Difficulty", Display.Difficulty(notification.Share.Difficulty)),
            new DiscordField("Of a block", Display.Percent(WorkMath.EffortPercent(notification.Share.Difficulty, notification.NetworkDifficulty))),
            new DiscordField("Network difficulty", Display.Difficulty(notification.NetworkDifficulty)),
            new DiscordField("Worker", Code(notification.Share.Worker), Inline: false),
        ],
        Footer(senderName),
        notification.Share.Timestamp);

    /// <summary>Formats miner-supplied text as inline code so it cannot inject Discord markup or mentions.</summary>
    private static string Code(string value) => $"`{value.Replace('`', '\'')}`";
}
