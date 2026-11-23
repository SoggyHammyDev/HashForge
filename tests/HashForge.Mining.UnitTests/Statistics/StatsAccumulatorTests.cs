using HashForge.Domain;
using HashForge.Mining.Statistics;

namespace HashForge.Mining.UnitTests.Statistics;

public sealed class StatsAccumulatorTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly StatsAccumulator _accumulator = new(Window);

    [Fact]
    public void GIVEN_SlidingWindowSum_WHEN_ValuesAddedInsideWindow_THEN_SumIncludesThem()
    {
        var window = new SlidingWindowSum(Window);

        window.Add(5, TestData.Now);
        window.Add(7, TestData.Now.AddMinutes(5));

        Assert.Equal(12, window.Sum(TestData.Now.AddMinutes(5)));
    }

    [Fact]
    public void GIVEN_SlidingWindowSum_WHEN_ValuesAgeOut_THEN_SumExcludesThem()
    {
        var window = new SlidingWindowSum(Window);
        window.Add(5, TestData.Now);
        window.Add(7, TestData.Now.AddMinutes(8));

        Assert.Equal(7, window.Sum(TestData.Now.AddMinutes(11)));
        Assert.Equal(0, window.Sum(TestData.Now.AddHours(1)));
    }

    [Fact]
    public void GIVEN_SlidingWindowSum_WHEN_SlotIsReusedAfterFullWindow_THEN_OldValueIsReplaced()
    {
        var window = new SlidingWindowSum(Window);
        window.Add(5, TestData.Now);

        window.Add(3, TestData.Now + Window);

        Assert.Equal(3, window.Sum(TestData.Now + Window));
    }

    [Fact]
    public void GIVEN_SlidingWindowSum_WHEN_WindowIsNotPositive_THEN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlidingWindowSum(TimeSpan.Zero));
    }

    [Fact]
    public void GIVEN_Record_WHEN_SharesAccepted_THEN_CountsEffortAndHashrateAccumulate()
    {
        _accumulator.Record(TestData.Share(credited: 300));
        _accumulator.Record(TestData.Share(credited: 300));

        var snapshot = _accumulator.Snapshot(TestData.Now, networkDifficulty: 1200);

        Assert.Equal(2, snapshot.Accepted);
        Assert.Equal(0, snapshot.Rejected);
        Assert.Equal(600, snapshot.RoundDifficulty);
        Assert.Equal(50, snapshot.EffortPercent);
        Assert.Equal(WorkMath.Hashrate(600, Window), snapshot.Hashrate);
        Assert.Equal(TestData.Now, snapshot.LastShareAt);
    }

    [Fact]
    public void GIVEN_Record_WHEN_SharesRejected_THEN_CountedByReasonWithoutEffort()
    {
        _accumulator.Record(TestData.Share(outcome: ShareOutcome.Duplicate));
        _accumulator.Record(TestData.Share(outcome: ShareOutcome.Duplicate));
        _accumulator.Record(TestData.Share(outcome: ShareOutcome.StaleJob));

        var snapshot = _accumulator.Snapshot(TestData.Now, 1000);

        Assert.Equal(0, snapshot.Accepted);
        Assert.Equal(3, snapshot.Rejected);
        Assert.Equal(2, snapshot.RejectedByReason[ShareOutcome.Duplicate]);
        Assert.Equal(1, snapshot.RejectedByReason[ShareOutcome.StaleJob]);
        Assert.Equal(0, snapshot.RoundDifficulty);
        Assert.Null(snapshot.BestShare);
    }

    [Fact]
    public void GIVEN_Record_WHEN_HigherDifficultyShareArrives_THEN_BestShareTracksItAndItsWorker()
    {
        var other = new WorkerIdentity("addr2", "rig9");
        _accumulator.Record(TestData.Share(actual: 500));
        _accumulator.Record(TestData.Share(worker: other, actual: 9000, timestamp: TestData.Now.AddSeconds(5)));
        _accumulator.Record(TestData.Share(actual: 700));

        var best = _accumulator.Snapshot(TestData.Now, 1).BestShare;

        Assert.Equal(new BestShare(9000, "addr2.rig9", TestData.Now.AddSeconds(5)), best);
    }

    [Fact]
    public void GIVEN_Record_WHEN_RejectedShareHasHighDifficulty_THEN_BestShareIgnoresIt()
    {
        _accumulator.Record(TestData.Share(outcome: ShareOutcome.Duplicate, actual: 1e12));

        Assert.Null(_accumulator.Snapshot(TestData.Now, 1).BestShare);
    }

    [Fact]
    public void GIVEN_ResetRound_WHEN_Called_THEN_EffortClearsButTotalsAndBestShareRemain()
    {
        _accumulator.Record(TestData.Share(credited: 300, actual: 400));

        _accumulator.ResetRound();

        var snapshot = _accumulator.Snapshot(TestData.Now, 1000);
        Assert.Equal(0, snapshot.RoundDifficulty);
        Assert.Equal(0, snapshot.EffortPercent);
        Assert.Equal(1, snapshot.Accepted);
        Assert.Equal(400, snapshot.BestShare!.Difficulty);
    }

    [Fact]
    public void GIVEN_Import_WHEN_GivenExportedState_THEN_CountersEffortAndBestShareAreRestored()
    {
        _accumulator.Record(TestData.Share(credited: 300, actual: 400));
        _accumulator.Record(TestData.Share(outcome: ShareOutcome.LowDifficulty));
        var restored = new StatsAccumulator(Window);

        restored.Import(_accumulator.Export());

        var expected = _accumulator.Snapshot(TestData.Now.AddHours(1), 1000);
        var actual = restored.Snapshot(TestData.Now.AddHours(1), 1000);
        Assert.Equal(expected.Accepted, actual.Accepted);
        Assert.Equal(expected.RejectedByReason, actual.RejectedByReason);
        Assert.Equal(expected.RoundDifficulty, actual.RoundDifficulty);
        Assert.Equal(expected.BestShare, actual.BestShare);
        Assert.Equal(expected.LastShareAt, actual.LastShareAt);
    }
}
