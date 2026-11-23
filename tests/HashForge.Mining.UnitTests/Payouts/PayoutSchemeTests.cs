using HashForge.Domain;
using HashForge.Mining.Payouts;
using Microsoft.Extensions.Time.Testing;

namespace HashForge.Mining.UnitTests.Payouts;

public sealed class PayoutSchemeTests
{
    private const double NetworkDifficulty = 100;

    private readonly FakeTimeProvider _time = new(TestData.Now);

    private ProportionalPayoutScheme Prop() => new(_time);

    private PplnsPayoutScheme Pplns(double multiplier = 2) => new(_time, multiplier);

    private void RecordEachMinute(IPayoutScheme scheme, params (string Address, double Difficulty)[] shares)
    {
        foreach (var (address, difficulty) in shares)
        {
            scheme.RecordShare(address, difficulty, NetworkDifficulty);
            _time.Advance(TimeSpan.FromMinutes(1));
        }
    }

    private static Dictionary<string, double> Weights(params (string Address, double Weight)[] entries) =>
        entries.ToDictionary(e => e.Address, e => e.Weight);

    [Fact]
    public void GIVEN_SoloGetPlan_WHEN_SharesAndBlocksHappen_THEN_OnlyRequestingMinerIsEverPaid()
    {
        var scheme = new SoloPayoutScheme();
        scheme.RecordShare("other", 50, NetworkDifficulty);
        scheme.OnBlockFound();

        var plan = scheme.GetPlan("me");

        Assert.Equal(Weights(("me", 1)), plan.Weights);
        Assert.Null(plan.FinderBonus);
        Assert.True(scheme.IsMinerSpecific);
        Assert.IsNotAssignableFrom<IPersistentPayoutScheme>(scheme);
    }

    [Fact]
    public void GIVEN_PropGetPlan_WHEN_SharesRecordedOverTime_THEN_WholeRoundIsSummedInOneBucket()
    {
        var scheme = Prop();

        RecordEachMinute(scheme, ("a", 10), ("b", 30), ("a", 500));

        Assert.Equal(Weights(("a", 510), ("b", 30)), scheme.GetPlan("ignored").Weights);
        Assert.Single(scheme.ExportState());
        Assert.False(scheme.IsMinerSpecific);
    }

    [Fact]
    public void GIVEN_PropOnBlockFound_WHEN_RoundHasShares_THEN_RoundIsClearedAndNextBlockPaysItsFinder()
    {
        var scheme = Prop();
        scheme.RecordShare("a", 10, NetworkDifficulty);

        scheme.OnBlockFound();

        Assert.True(scheme.IsMinerSpecific);
        Assert.Equal(Weights(("finder", 1)), scheme.GetPlan("finder").Weights);
    }

    [Fact]
    public void GIVEN_PplnsOnBlockFound_WHEN_WindowHasShares_THEN_WindowIsKept()
    {
        var scheme = Pplns();
        scheme.RecordShare("a", 10, NetworkDifficulty);

        scheme.OnBlockFound();

        Assert.Equal(Weights(("a", 10)), scheme.GetPlan("a").Weights);
    }

    [Fact]
    public void GIVEN_PplnsGetPlan_WHEN_NoHistoryYet_THEN_PaysTheRequestingMiner()
    {
        var scheme = Pplns();

        Assert.True(scheme.IsMinerSpecific);
        Assert.Equal(Weights(("finder", 1)), scheme.GetPlan("finder").Weights);
    }

    [Fact]
    public void GIVEN_PplnsRecordShare_WHEN_WindowOverflows_THEN_OldestBucketsExpire()
    {
        var scheme = Pplns(multiplier: 2);

        // Window is 200. Buckets: old=150, a=150, b=150, then a newer share triggers the trim.
        RecordEachMinute(scheme, ("old", 150), ("a", 150), ("b", 150), ("c", 1));

        Assert.Equal(Weights(("a", 150), ("b", 150), ("c", 1)), scheme.GetPlan("ignored").Weights);
    }

    [Fact]
    public void GIVEN_PplnsRecordShare_WHEN_RemainingBucketsWouldNotCoverWindow_THEN_OldestIsKept()
    {
        var scheme = Pplns(multiplier: 2);

        RecordEachMinute(scheme, ("old", 150), ("a", 100), ("b", 1));

        Assert.Contains("old", scheme.GetPlan("ignored").Weights.Keys);
    }

    [Fact]
    public void GIVEN_PplnsRecordShare_WHEN_NetworkDifficultyUnknown_THEN_NothingExpires()
    {
        var scheme = Pplns();
        foreach (var address in new[] { "a", "b", "c" })
        {
            scheme.RecordShare(address, 1000, networkDifficulty: 0);
            _time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(3, scheme.GetPlan("ignored").Weights.Count);
    }

    [Fact]
    public void GIVEN_PplnsRecordShare_WHEN_SharesArriveWithinSameMinute_THEN_TheyShareOneBucket()
    {
        var scheme = Pplns();

        scheme.RecordShare("a", 1, NetworkDifficulty);
        _time.Advance(TimeSpan.FromSeconds(30));
        scheme.RecordShare("b", 1, NetworkDifficulty);

        Assert.Single(scheme.ExportState());
    }

    [Fact]
    public void GIVEN_PplnsRecordShare_WHEN_PoolNeverFillsWindow_THEN_BucketCountStaysBoundedAndNoWorkIsLost()
    {
        var scheme = Pplns(multiplier: 1_000_000);
        const int Shares = PplnsPayoutScheme.MaxBuckets + 50;

        for (var i = 0; i < Shares; i++)
        {
            scheme.RecordShare("a", 1, NetworkDifficulty);
            _time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.True(scheme.ExportState().Count <= PplnsPayoutScheme.MaxBuckets);
        Assert.Equal(Shares, scheme.GetPlan("a").Weights["a"]);
        Assert.Equal(Shares, scheme.ExportState().Sum(b => b.Shares["a"]));
    }

    [Fact]
    public void GIVEN_PplnsRecordShare_WHEN_MinersWorkExpiresWithFractionalDifficulties_THEN_NoPhantomWeightRemains()
    {
        var scheme = Pplns(multiplier: 0.001);

        RecordEachMinute(scheme, ("gone", 0.1), ("gone", 0.2), ("stay", 500), ("stay", 500), ("stay", 500));

        Assert.DoesNotContain("gone", scheme.GetPlan("x").Weights.Keys);
    }

    [Fact]
    public void GIVEN_ImportState_WHEN_GivenExportedState_THEN_WeightsAndExpiryAreRestored()
    {
        var original = Pplns(multiplier: 2);
        RecordEachMinute(original, ("old", 150), ("a", 150));

        var restored = Pplns(multiplier: 2);
        restored.RecordShare("stale", 999, NetworkDifficulty);
        restored.ImportState(original.ExportState());

        Assert.Equal(original.GetPlan("x").Weights, restored.GetPlan("x").Weights);
        RecordEachMinute(restored, ("b", 150), ("c", 1));
        Assert.DoesNotContain("old", restored.GetPlan("x").Weights.Keys);
    }

    [Fact]
    public void GIVEN_GetPlan_WHEN_ResultIsModified_THEN_SchemeIsUnaffected()
    {
        var scheme = Prop();
        scheme.RecordShare("a", 10, NetworkDifficulty);

        ((Dictionary<string, double>)scheme.GetPlan("a").Weights).Clear();

        Assert.Single(scheme.GetPlan("a").Weights);
    }

    [Fact]
    public void GIVEN_FinderBonusGetPlan_WHEN_RoundHasShares_THEN_SplitsLikeInnerSchemeAndNamesRequesterForBonus()
    {
        var scheme = new FinderBonusPayoutScheme(Prop(), bonusSatoshis: 100_000_000);
        scheme.RecordShare("a", 3, NetworkDifficulty);
        scheme.RecordShare("b", 1, NetworkDifficulty);

        var plan = scheme.GetPlan("b");

        Assert.Equal(Weights(("a", 3), ("b", 1)), plan.Weights);
        Assert.Equal(new FinderBonus("b", 100_000_000), plan.FinderBonus);
        Assert.True(scheme.IsMinerSpecific);
    }

    [Fact]
    public void GIVEN_FinderBonus_WHEN_BlockFoundAndStateRoundTripped_THEN_DelegatesToInnerScheme()
    {
        var scheme = new FinderBonusPayoutScheme(Prop(), bonusSatoshis: 1);
        scheme.RecordShare("a", 3, NetworkDifficulty);
        var saved = scheme.ExportState();

        scheme.OnBlockFound();
        Assert.Equal(["finder"], scheme.GetPlan("finder").Weights.Keys);

        scheme.ImportState(saved);
        Assert.Equal(Weights(("a", 3)), scheme.GetPlan("finder").Weights);
    }

    [Theory]
    [InlineData(PayoutScheme.Solo, typeof(SoloPayoutScheme))]
    [InlineData(PayoutScheme.Prop, typeof(ProportionalPayoutScheme))]
    [InlineData(PayoutScheme.Pplns, typeof(PplnsPayoutScheme))]
    [InlineData(PayoutScheme.FinderBonus, typeof(FinderBonusPayoutScheme))]
    public void GIVEN_Create_WHEN_GivenScheme_THEN_ReturnsMatchingImplementation(PayoutScheme scheme, Type expected)
    {
        var created = new PayoutSchemeFactory(_time).Create(TestData.Pool(scheme: scheme));

        Assert.IsType(expected, created);
    }

    [Fact]
    public void GIVEN_Create_WHEN_SchemeIsFinderBonus_THEN_UsesThePoolsConfiguredBonus()
    {
        var pool = TestData.Pool(scheme: PayoutScheme.FinderBonus) with { FinderBonusSatoshis = 25_000_000 };

        var plan = new PayoutSchemeFactory(_time).Create(pool).GetPlan("finder");

        Assert.Equal(new FinderBonus("finder", 25_000_000), plan.FinderBonus);
    }

    [Fact]
    public void GIVEN_Create_WHEN_SchemeIsUnknown_THEN_Throws()
    {
        var factory = new PayoutSchemeFactory(_time);

        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(TestData.Pool(scheme: (PayoutScheme)99)));
    }
}
