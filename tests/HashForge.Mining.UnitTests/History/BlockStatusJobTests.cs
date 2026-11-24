using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Network;
using HashForge.Mining.Shares;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.UnitTests.History;

public sealed class BlockStatusJobTests
{
    private const int TipHeight = 850_000;

    private readonly IBlockRepository _repository = Substitute.For<IBlockRepository>();
    private readonly IBlockHeaderSource _headers = Substitute.For<IBlockHeaderSource>();
    private readonly INetworkStatsStore _network = Substitute.For<INetworkStatsStore>();
    private readonly BlockStatusJob _job;

    public BlockStatusJobTests()
    {
        _network.Current.Returns(new NetworkStats("main", TipHeight, TipHeight, 1, 2, "00", false, 1, true, 5, TestData.Now));
        _job = new BlockStatusJob(
            _repository,
            _headers,
            _network,
            Options.Create(new MiningOptions { NetworkStatsRefreshSeconds = 45 }),
            NullLogger<BlockStatusJob>.Instance);
    }

    private static FoundBlock Block(string hash, int confirmations = 0, bool orphaned = false) => new(
        "solo", PayoutScheme.Solo, TipHeight - 1, hash, TestData.Worker, 312_500_000, [], 50, TestData.Now, true, null)
    {
        Confirmations = confirmations,
        Orphaned = orphaned,
    };

    private void Unsettled(params FoundBlock[] blocks) =>
        _repository.GetUnsettledAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(blocks);

    private void NodeSays(string hash, int confirmations) =>
        _headers.FindBlockHeaderAsync(hash, Arg.Any<CancellationToken>()).Returns(new BlockHeaderInfo { Confirmations = confirmations });

    [Fact]
    public void GIVEN_Job_WHEN_Created_THEN_RunsAsOftenAsNetworkStats()
    {
        Assert.Equal("block-status", _job.Name);
        Assert.Equal(TimeSpan.FromSeconds(45), _job.Interval);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NodeHasNotAnsweredYet_THEN_NothingIsChecked()
    {
        _network.Current.Returns((NetworkStats?)null);

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.DidNotReceiveWithAnyArgs().GetUnsettledAsync(default, default);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_Run_THEN_OnlyBlocksWithinTheWatchDepthAreAskedFor()
    {
        Unsettled();

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.Received(1).GetUnsettledAsync(TipHeight - BlockStatusJob.WatchDepth, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_BlockIsNoLongerOnTheBestChain_THEN_ItIsMarkedOrphaned()
    {
        Unsettled(Block("aa", confirmations: 2));
        NodeSays("aa", -1);

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.Received(1).UpdateChainStateAsync("aa", 0, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_BlockHasGainedConfirmations_THEN_TheCountIsStored()
    {
        Unsettled(Block("aa", confirmations: 2));
        NodeSays("aa", 5);

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.Received(1).UpdateChainStateAsync("aa", 5, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_OrphanedBlockReturnsToTheChain_THEN_ItIsNoLongerOrphaned()
    {
        Unsettled(Block("aa", orphaned: true));
        NodeSays("aa", 3);

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.Received(1).UpdateChainStateAsync("aa", 3, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NothingHasChanged_THEN_NothingIsWritten()
    {
        Unsettled(Block("aa", confirmations: 4), Block("bb", orphaned: true));
        NodeSays("aa", 4);
        NodeSays("bb", -1);

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.DidNotReceiveWithAnyArgs().UpdateChainStateAsync(default!, default, default, default);
    }

    [Fact]
    public async Task GIVEN_ExecuteAsync_WHEN_NodeHasNeverHeardOfABlock_THEN_ItIsLeftAloneAndTheRestAreStillChecked()
    {
        Unsettled(Block("aa"), Block("bb"));
        _headers.FindBlockHeaderAsync("aa", Arg.Any<CancellationToken>()).Returns((BlockHeaderInfo?)null);
        NodeSays("bb", 1);

        await _job.ExecuteAsync(CancellationToken.None);

        await _repository.DidNotReceive().UpdateChainStateAsync("aa", Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateChainStateAsync("bb", 1, false, Arg.Any<CancellationToken>());
    }
}
