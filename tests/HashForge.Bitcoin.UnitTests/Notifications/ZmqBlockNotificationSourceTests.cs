using HashForge.Bitcoin.Notifications;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;

namespace HashForge.Bitcoin.UnitTests.Notifications;

public sealed class ZmqBlockNotificationSourceTests
{
    private const string BlockHash = "00000000000000000001aa0000000000000000000000000000000000000000ff";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static ZmqBlockNotificationSource Source(string url) =>
        new(Options.Create(new BitcoinNodeOptions { ZmqBlockUrl = url }));

    /// <summary>Publishes like Bitcoin Core does: topic, 32-byte hash, 4-byte sequence number.</summary>
    private static void Publish(PublisherSocket publisher, string topic, string hashHex) =>
        publisher.SendMoreFrame(topic).SendMoreFrame(Convert.FromHexString(hashHex)).SendFrame(new byte[4]);

    /// <summary>Keeps publishing until the subscriber has joined; ZeroMQ drops messages sent before then.</summary>
    private static async Task<string> FirstHashAsync(ZmqBlockNotificationSource source, Action publish)
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        var listening = Task.Run(async () =>
        {
            await foreach (var hash in source.ListenAsync(cancellation.Token))
            {
                return hash;
            }

            throw new InvalidOperationException("The feed ended without a block.");
        });

        while (!listening.IsCompleted)
        {
            publish();
            await Task.Delay(20, cancellation.Token);
        }

        var result = await listening;
        await cancellation.CancelAsync();
        return result;
    }

    [Fact]
    public async Task GIVEN_ListenAsync_WHEN_NodePublishesHashblock_THEN_YieldsTheBlockHash()
    {
        using var publisher = new PublisherSocket();
        var port = publisher.BindRandomPort("tcp://127.0.0.1");
        var source = Source($"tcp://127.0.0.1:{port}");

        var hash = await FirstHashAsync(source, () => Publish(publisher, ZmqBlockNotificationSource.Topic, BlockHash));

        Assert.Equal(BlockHash, hash);
        Assert.True(source.IsEnabled);
    }

    [Fact]
    public async Task GIVEN_ListenAsync_WHEN_NodePublishesOtherTopics_THEN_OnlyBlockAnnouncementsAreYielded()
    {
        using var publisher = new PublisherSocket();
        var port = publisher.BindRandomPort("tcp://127.0.0.1");
        var source = Source($"tcp://127.0.0.1:{port}");
        var other = new string('9', 64);

        var hash = await FirstHashAsync(source, () =>
        {
            Publish(publisher, "hashtx", other);
            Publish(publisher, "rawblock", other);
            Publish(publisher, ZmqBlockNotificationSource.Topic, BlockHash);
        });

        Assert.Equal(BlockHash, hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GIVEN_ListenAsync_WHEN_NoAddressConfigured_THEN_IsDisabledAndYieldsNothing(string url)
    {
        var source = Source(url);
        var hashes = new List<string>();

        await foreach (var hash in source.ListenAsync(CancellationToken.None))
        {
            hashes.Add(hash);
        }

        Assert.False(source.IsEnabled);
        Assert.Empty(hashes);
    }

    [Fact]
    public async Task GIVEN_ListenAsync_WHEN_Cancelled_THEN_StopsPromptly()
    {
        using var publisher = new PublisherSocket();
        var port = publisher.BindRandomPort("tcp://127.0.0.1");
        using var cancellation = new CancellationTokenSource();
        var listening = Task.Run(async () =>
        {
            await foreach (var _ in Source($"tcp://127.0.0.1:{port}").ListenAsync(cancellation.Token))
            {
            }
        });

        await Task.Delay(100);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listening.WaitAsync(Timeout));
    }
}