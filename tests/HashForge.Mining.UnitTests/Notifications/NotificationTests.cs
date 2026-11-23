using System.Net;
using System.Text.Json;
using HashForge.Mining.Notifications;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.UnitTests.Notifications;

public sealed class NotificationTests
{
    private const string WebhookUrl = "https://discord.example/api/webhooks/1/secret-token";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly NotificationOptions _options = new();

    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    private (DiscordWebhookChannel Channel, CapturingHandler Handler) Discord(HttpStatusCode status = HttpStatusCode.NoContent)
    {
        var handler = new CapturingHandler(status);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(DiscordWebhookChannel.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return (new DiscordWebhookChannel(factory, Options.Create(_options)), handler);
    }








    [Fact]
    public async Task GIVEN_NotificationOutbox_WHEN_ItemsEnqueued_THEN_TheyAreReadInOrder()
    {
        var queue = new NotificationOutbox();
        var first = new BlockFoundNotification(TestData.Block());
        var second = new BestShareNotification("p", new BestShare(1, "w", TestData.Now), 1);
        queue.Enqueue(first);
        queue.Enqueue(second);

        var read = new List<PoolNotification>();
        await foreach (var item in queue.ReadAllAsync(CancellationToken.None))
        {
            read.Add(item);
            if (read.Count == 2)
            {
                break;
            }
        }

        Assert.Equal([first, second], read);
    }

    [Fact]
    public async Task GIVEN_NotificationOutbox_WHEN_Overfilled_THEN_OldestAreDroppedAndNewestKept()
    {
        var queue = new NotificationOutbox();
        var newest = new BestShareNotification("newest", new BestShare(1, "w", TestData.Now), 1);

        for (var i = 0; i < 1000; i++)
        {
            queue.Enqueue(new BlockFoundNotification(TestData.Block()));
        }

        queue.Enqueue(newest);

        var kept = new List<PoolNotification>();
        using var cancellation = new CancellationTokenSource();
        await foreach (var item in queue.ReadAllAsync(cancellation.Token))
        {
            kept.Add(item);
            if (ReferenceEquals(item, newest))
            {
                break;
            }
        }

        Assert.InRange(kept.Count, 2, 100);
        Assert.Same(newest, kept[^1]);
    }

    [Fact]
    public void GIVEN_CreateMessage_WHEN_BlockFound_THEN_EmbedShowsHeightRewardEffortFinderAndHash()
    {
        var message = DiscordMessageFactory.Create(new BlockFoundNotification(TestData.Block()), "MyPool");

        var embed = Assert.Single(message.Embeds);
        Assert.Equal("MyPool", message.Username);
        Assert.Equal("🎉 Block found!", embed.Title);
        Assert.Contains("**pplns**", embed.Description);
        Assert.Contains("**850,000**", embed.Description);
        Assert.Equal(DiscordMessageFactory.BlockFoundColor, embed.Color);
        Assert.Equal(TestData.Now, embed.Timestamp);
        Assert.Equal(
            [("Reward", "3.12500000 BTC"), ("Effort", "42%"), ("Payout", "PPLNS · 1 miners paid"), ("Found by", "`miner-address.rig1`"), ("Block hash", "`00ab`")],
            embed.Fields.Select(f => (f.Name, f.Value)));
        Assert.False(embed.Fields[3].Inline);
    }

    [Fact]
    public void GIVEN_CreateMessage_WHEN_BestShare_THEN_EmbedShowsDifficultyAndShareOfBlock()
    {
        var notification = new BestShareNotification("solo", new BestShare(2.5e9, "bc1q`evil`.rig", TestData.Now), 1e10);

        var embed = Assert.Single(DiscordMessageFactory.Create(notification, "HashForge").Embeds);

        Assert.Equal("💎 New best share", embed.Title);
        Assert.Equal(DiscordMessageFactory.BestShareColor, embed.Color);
        Assert.Equal(
            [("Difficulty", "2.5 G"), ("Of a block", "25%"), ("Network difficulty", "10 G"), ("Worker", "`bc1q'evil'.rig`")],
            embed.Fields.Select(f => (f.Name, f.Value)));
    }

    [Fact]
    public void GIVEN_CreateMessage_WHEN_NotificationTypeIsUnknown_THEN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DiscordMessageFactory.Create(new UnknownNotification(), "x"));
    }

    private sealed record UnknownNotification : PoolNotification;

    [Fact]
    public async Task GIVEN_DiscordSendAsync_WHEN_WebhookConfigured_THEN_PostsEmbedJsonToWebhook()
    {
        _options.DiscordWebhookUrl = WebhookUrl;
        var (channel, handler) = Discord();

        await channel.SendAsync(new BlockFoundNotification(TestData.Block()), CancellationToken.None);

        Assert.Equal(new Uri(WebhookUrl), handler.RequestUri);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("HashForge", body.RootElement.GetProperty("username").GetString());
        var embed = body.RootElement.GetProperty("embeds")[0];
        Assert.Equal(0xF7931A, embed.GetProperty("color").GetInt32());
        Assert.Equal("Reward", embed.GetProperty("fields")[0].GetProperty("name").GetString());
        Assert.True(embed.GetProperty("fields")[0].GetProperty("inline").GetBoolean());
        Assert.Equal("HashForge mining pool", embed.GetProperty("footer").GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GIVEN_DiscordSendAsync_WHEN_NoWebhookConfigured_THEN_NothingIsSent(string? url)
    {
        _options.DiscordWebhookUrl = url;
        var (channel, handler) = Discord();

        await channel.SendAsync(new BlockFoundNotification(TestData.Block()), CancellationToken.None);

        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task GIVEN_DiscordSendAsync_WHEN_DiscordRejectsMessage_THEN_ThrowsWithoutRevealingWebhookUrl()
    {
        _options.DiscordWebhookUrl = WebhookUrl;
        var (channel, _) = Discord(HttpStatusCode.TooManyRequests);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => channel.SendAsync(new BlockFoundNotification(TestData.Block()), CancellationToken.None));

        Assert.Contains("429", exception.Message);
        Assert.DoesNotContain("secret-token", exception.ToString());
    }

    [Fact]
    public async Task GIVEN_Dispatcher_WHEN_OneChannelFails_THEN_OtherChannelsAndLaterNotificationsStillDeliver()
    {
        var queue = new NotificationOutbox();
        var failing = Substitute.For<INotificationChannel>();
        failing.SendAsync(Arg.Any<PoolNotification>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new HttpRequestException("down")));
        var delivered = System.Threading.Channels.Channel.CreateUnbounded<PoolNotification>();
        var healthy = Substitute.For<INotificationChannel>();
        healthy.SendAsync(Arg.Any<PoolNotification>(), Arg.Any<CancellationToken>())
            .Returns(call => delivered.Writer.WriteAsync(call.Arg<PoolNotification>()).AsTask());
        using var dispatcher = new NotificationDispatcher(queue, [failing, healthy], NullLogger<NotificationDispatcher>.Instance);
        await dispatcher.StartAsync(CancellationToken.None);

        var first = new BlockFoundNotification(TestData.Block());
        var second = new BestShareNotification("p", new BestShare(1, "w", TestData.Now), 1);
        queue.Enqueue(first);
        queue.Enqueue(second);

        Assert.Same(first, await delivered.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        Assert.Same(second, await delivered.Reader.ReadAsync().AsTask().WaitAsync(Timeout));
        await dispatcher.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(WebhookUrl, true)]
    [InlineData("http://insecure.example/hook", false)]
    [InlineData("not a url", false)]
    public void GIVEN_OptionsValidate_WHEN_WebhookUrlGiven_THEN_OnlyEmptyOrHttpsIsAccepted(string? url, bool expectedValid)
    {
        var result = new NotificationOptionsValidator().Validate(null, new NotificationOptions { DiscordWebhookUrl = url });

        Assert.Equal(expectedValid, result.Succeeded);
    }

    [Fact]
    public void GIVEN_OptionsValidate_WHEN_CooldownIsNegative_THEN_Fails()
    {
        var result = new NotificationOptionsValidator().Validate(null, new NotificationOptions { BestShareCooldownSeconds = -1 });

        Assert.Contains("BestShareCooldownSeconds", result.FailureMessage);
    }
}
