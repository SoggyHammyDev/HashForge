using System.Numerics;
using HashForge.Bitcoin.Primitives;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Jobs;
using HashForge.Mining.Payouts;

namespace HashForge.Mining.UnitTests.Jobs;

public sealed class JobTests
{
    private readonly IPayoutPlanSource _weights = Substitute.For<IPayoutPlanSource>();
    private readonly IAddressScriptResolver _resolver = Substitute.For<IAddressScriptResolver>();
    private readonly IJobFactory _factoryStub = Substitute.For<IJobFactory>();
    private readonly PoolDefinition _shared = TestData.Pool("pplns", PayoutScheme.Pplns, 1);
    private readonly PoolDefinition _solo = TestData.Pool("solo", PayoutScheme.Solo, 2);

    public JobTests()
    {
        _resolver.ResolveAsync(TestData.MinerAddress, Arg.Any<CancellationToken>()).Returns(new AddressResolution(true, [0x51], null));
        _resolver.ResolveAsync(TestData.FeeAddress, Arg.Any<CancellationToken>()).Returns(new AddressResolution(true, [0x52], null));
        _weights.GetPlan(Arg.Any<PoolDefinition>(), Arg.Any<string>()).Returns(new PayoutPlan(new Dictionary<string, double> { [TestData.MinerAddress] = 1 }));
        _weights.IsMinerSpecific(Arg.Any<PoolDefinition>()).Returns(call => call.Arg<PoolDefinition>().Scheme == PayoutScheme.Solo);
        _factoryStub.CreateAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(call => TestData.Job(call.Arg<PoolDefinition>(), call.Arg<BlockTemplate>(), call.ArgAt<string>(0)));
    }

    private JobFactory Factory() => new(_weights, _resolver, TestData.Options());

    [Fact]
    public void GIVEN_MiningJob_WHEN_Constructed_THEN_DerivesHeaderFieldsFromTemplate()
    {
        var template = TestData.Template(transactionCount: 3);

        var job = TestData.Job(template: template);

        Assert.Equal(0x1d00ffffu, job.Bits);
        Assert.Equal(DifficultyMath.Diff1Target, job.NetworkTarget);
        Assert.Equal(HashHex.FromDisplayHex(TestData.PreviousBlockHash), job.PreviousBlockHash);
        Assert.Equal(2, job.MerkleBranch.Count);
    }

    [Fact]
    public void GIVEN_TryRegisterSubmission_WHEN_SameKeySubmittedTwice_THEN_SecondIsRefusedIgnoringCase()
    {
        var job = TestData.Job();

        Assert.True(job.TryRegisterSubmission("AB:01"));
        Assert.False(job.TryRegisterSubmission("ab:01"));
        Assert.True(job.TryRegisterSubmission("ab:02"));
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_PoolHasFee_THEN_CoinbasePaysMinerAndFeeAddress()
    {
        var template = TestData.Template();

        var job = await Factory().CreateAsync("7", _shared, template, TestData.MinerAddress, CancellationToken.None);

        Assert.Equal("7", job.Id);
        Assert.Equal(
            [new PayoutEntry(TestData.MinerAddress, 309_375_000), new PayoutEntry(TestData.FeeAddress, 3_125_000)],
            job.Payouts);
        var suffix = Convert.ToHexStringLower(job.Coinbase.Suffix);
        Assert.Contains("02" + "18b0701200000000" + "0151" + "08af2f0000000000" + "0152", suffix);
        Assert.Equal(JobFactory.Extranonce1Size + 8, job.Coinbase.ExtranonceSize);
        Assert.Equal(1, job.MinersPaid);
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_FeeAddressAlsoMined_THEN_ItCountsAsMinerPaid()
    {
        _weights.GetPlan(Arg.Any<PoolDefinition>(), Arg.Any<string>()).Returns(new PayoutPlan(new Dictionary<string, double> { [TestData.MinerAddress] = 1, [TestData.FeeAddress] = 1 }));

        var job = await Factory().CreateAsync("1", _shared, TestData.Template(), TestData.MinerAddress, CancellationToken.None);

        Assert.Equal(2, job.Payouts.Count);
        Assert.Equal(2, job.MinersPaid);
    }

    [Fact]
    public void GIVEN_MiningJob_WHEN_MinersPaidNotSupplied_THEN_DefaultsToPayoutCount()
    {
        Assert.Equal(1, TestData.Job().MinersPaid);
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_TemplateHasWitnessCommitment_THEN_CommitmentOutputIsIncluded()
    {
        var template = TestData.Template(witnessCommitment: "6a24aa21a9ed");

        var job = await Factory().CreateAsync("1", _shared, template, TestData.MinerAddress, CancellationToken.None);

        Assert.Contains("0000000000000000" + "06" + "6a24aa21a9ed", Convert.ToHexStringLower(job.Coinbase.Suffix));
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_Called_THEN_WeightsAreRequestedForThatMiner()
    {
        await Factory().CreateAsync("1", _solo, TestData.Template(), TestData.MinerAddress, CancellationToken.None);

        _weights.Received(1).GetPlan(_solo, TestData.MinerAddress);
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_PayoutAddressIsInvalid_THEN_ThrowsWithNodeReason()
    {
        _resolver.ResolveAsync(TestData.FeeAddress, Arg.Any<CancellationToken>()).Returns(new AddressResolution(false, null, "bad checksum"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Factory().CreateAsync("1", _shared, TestData.Template(), TestData.MinerAddress, CancellationToken.None));

        Assert.Contains("bad checksum", exception.Message);
    }

    [Fact]
    public async Task GIVEN_GetCurrentJobAsync_WHEN_NoTemplatePublished_THEN_ReturnsNull()
    {
        var manager = new JobManager(_factoryStub, _weights);

        Assert.Null(await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None));
    }

    [Fact]
    public async Task GIVEN_GetCurrentJobAsync_WHEN_PoolIsShared_THEN_AllMinersGetSameJob()
    {
        var manager = new JobManager(_factoryStub, _weights);
        manager.Publish(TestData.Template());

        var first = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);
        var second = await manager.GetCurrentJobAsync(_shared, "b", CancellationToken.None);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GIVEN_GetCurrentJobAsync_WHEN_PoolIsSolo_THEN_EachMinerGetsOwnJobReusedPerMiner()
    {
        var manager = new JobManager(_factoryStub, _weights);
        manager.Publish(TestData.Template());

        var first = await manager.GetCurrentJobAsync(_solo, "a", CancellationToken.None);
        var again = await manager.GetCurrentJobAsync(_solo, "a", CancellationToken.None);
        var other = await manager.GetCurrentJobAsync(_solo, "b", CancellationToken.None);

        Assert.Same(first, again);
        Assert.NotSame(first, other);
        Assert.NotEqual(first!.Id, other!.Id);
    }

    [Fact]
    public async Task GIVEN_GetCurrentJobAsync_WHEN_SharedPoolHasNoHistoryYet_THEN_EachMinerGetsOwnJob()
    {
        _weights.IsMinerSpecific(_shared).Returns(true);
        var manager = new JobManager(_factoryStub, _weights);
        manager.Publish(TestData.Template());

        var first = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);
        var other = await manager.GetCurrentJobAsync(_shared, "b", CancellationToken.None);

        Assert.NotSame(first, other);
    }

    [Fact]
    public async Task GIVEN_Find_WHEN_JobWasIssued_THEN_ReturnsIt()
    {
        var manager = new JobManager(_factoryStub, _weights);
        manager.Publish(TestData.Template());
        var job = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);

        Assert.Same(job, manager.Find(job!.Id));
        Assert.Null(manager.Find("unknown"));
    }

    [Fact]
    public async Task GIVEN_Publish_WHEN_TemplateRefreshedOnSameTip_THEN_NewJobIssuedAndOldJobStillValid()
    {
        var manager = new JobManager(_factoryStub, _weights);
        var events = new List<bool>();
        manager.JobsChanged += (_, args) => events.Add(args.IsNewBlock);
        manager.Publish(TestData.Template());
        var old = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);

        manager.Publish(TestData.Template());
        var fresh = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);

        Assert.NotSame(old, fresh);
        Assert.Same(old, manager.Find(old!.Id));
        Assert.Equal([true, false], events);
    }

    [Fact]
    public async Task GIVEN_Publish_WHEN_ChainTipChanges_THEN_OldJobsBecomeStale()
    {
        var manager = new JobManager(_factoryStub, _weights);
        manager.Publish(TestData.Template());
        var old = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);
        var isNewBlock = false;
        manager.JobsChanged += (_, args) => isNewBlock = args.IsNewBlock;

        manager.Publish(TestData.Template(previousBlockHash: new string('1', 64)));

        Assert.True(isNewBlock);
        Assert.Null(manager.Find(old!.Id));
    }

    [Fact]
    public void GIVEN_MiningJob_WHEN_BitsAreRegtest_THEN_NetworkTargetIsRegtestTarget()
    {
        var job = TestData.Job(template: TestData.Template(bits: TestData.RegtestBits));

        Assert.Equal(new BigInteger(0x7fffff) << 232, job.NetworkTarget);
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_PlanHasFinderBonus_THEN_FinderGetsBonusOnTopOfShare()
    {
        var pool = TestData.Pool("bonus", PayoutScheme.FinderBonus, 3) with { FinderBonusSatoshis = 100_000_000, FeePercent = 0 };
        _resolver.ResolveAsync("other", Arg.Any<CancellationToken>()).Returns(new AddressResolution(true, [0x53], null));
        _weights.GetPlan(pool, TestData.MinerAddress).Returns(new PayoutPlan(
            new Dictionary<string, double> { [TestData.MinerAddress] = 1, ["other"] = 1 },
            new FinderBonus(TestData.MinerAddress, 100_000_000)));

        var job = await Factory().CreateAsync("1", pool, TestData.Template(), TestData.MinerAddress, CancellationToken.None);

        // 3.125 BTC: 1 BTC bonus to the finder, then 2.125 BTC split evenly.
        Assert.Equal(
            [new PayoutEntry(TestData.MinerAddress, 206_250_000), new PayoutEntry("other", 106_250_000)],
            job.Payouts);
        Assert.Equal(2, job.MinersPaid);
    }

    [Fact]
    public async Task GIVEN_CreateAsync_WHEN_PlanHasNoFinderBonus_THEN_NoBonusIsPaid()
    {
        var pool = TestData.Pool("prop", PayoutScheme.Prop, 3) with { FinderBonusSatoshis = 100_000_000, FeePercent = 0 };

        var job = await Factory().CreateAsync("1", pool, TestData.Template(), TestData.MinerAddress, CancellationToken.None);

        Assert.Equal([new PayoutEntry(TestData.MinerAddress, 312_500_000)], job.Payouts);
    }

    [Fact]
    public async Task GIVEN_GetCurrentJobAsync_WHEN_JobCreationFailsOnce_THEN_NextRequestTriesAgainOnSameTemplate()
    {
        var attempts = 0;
        _factoryStub.CreateAsync(default!, default!, default!, default!, default).ReturnsForAnyArgs(call =>
            Interlocked.Increment(ref attempts) == 1
                ? Task.FromException<MiningJob>(new HttpRequestException("node hiccup"))
                : Task.FromResult(TestData.Job(call.Arg<PoolDefinition>(), call.Arg<BlockTemplate>(), call.ArgAt<string>(0))));
        var manager = new JobManager(_factoryStub, _weights);
        manager.Publish(TestData.Template());

        await Assert.ThrowsAsync<HttpRequestException>(() => manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None));
        var job = await manager.GetCurrentJobAsync(_shared, "a", CancellationToken.None);

        Assert.NotNull(job);
        Assert.Equal(2, attempts);
    }
}