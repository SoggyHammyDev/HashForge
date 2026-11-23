using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.UnitTests.Sessions;

public sealed class SessionPartsTests
{
    [Fact]
    public void GIVEN_ShareTally_WHEN_SharesCounted_THEN_AcceptedAndRejectedAreKeptApart()
    {
        var tally = new ShareTally();

        tally.Count(accepted: true);
        tally.Count(accepted: true);
        tally.Count(accepted: false);

        Assert.Equal((2L, 1L), (tally.Accepted, tally.Rejected));
    }

    [Fact]
    public async Task GIVEN_MinerChannelSendAsync_WHEN_ComposerReturnsNull_THEN_NothingIsWritten()
    {
        var connection = new FakeConnection();
        var channel = new MinerChannel(connection, TimeSpan.FromSeconds(30));

        await channel.SendAsync(() => null, CancellationToken.None);
        await channel.SendAsync(() => "sent", CancellationToken.None);

        Assert.Equal(["sent"], connection.Sent);
    }

    [Fact]
    public async Task GIVEN_MinerChannelSendAsync_WHEN_SendersRace_THEN_ComposersRunOneAtATimeInWriteOrder()
    {
        var connection = new FakeConnection();
        var channel = new MinerChannel(connection, TimeSpan.FromSeconds(30));
        var counter = 0;

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => channel.SendAsync(() => (++counter).ToString(), CancellationToken.None))));

        Assert.Equal(Enumerable.Range(1, 100).Select(i => i.ToString()), connection.Sent);
    }

    [Fact]
    public async Task GIVEN_MinerChannel_WHEN_Used_THEN_ReceivesLinesAndReportsAddressAndDisposesConnection()
    {
        var connection = new FakeConnection();
        var channel = new MinerChannel(connection, TimeSpan.FromSeconds(30));
        connection.MinerSends("hello");

        Assert.Equal("hello", await channel.ReceiveAsync(CancellationToken.None));
        Assert.Equal(connection.RemoteAddress, channel.RemoteAddress);

        await channel.DisposeAsync();
        Assert.True(connection.Disposed);
    }
}