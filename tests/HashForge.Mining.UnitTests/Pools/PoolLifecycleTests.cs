using HashForge.Domain;
using HashForge.Mining.Payouts;
using HashForge.Mining.Pools;
using Microsoft.Extensions.Logging.Abstractions;

namespace HashForge.Mining.UnitTests.Pools;

public sealed class PoolLifecycleTests
{
    private readonly PoolRegistry _registry = new();
    private readonly IPoolDefinitionStore _store = Substitute.For<IPoolDefinitionStore>();
    private readonly IAddressScriptResolver _resolver = Substitute.For<IAddressScriptResolver>();
    private readonly PoolProvisioner _provisioner;

    public PoolLifecycleTests()
    {
        _resolver.ResolveAsync(TestData.FeeAddress, Arg.Any<CancellationToken>())
            .Returns(new AddressResolution(true, [0x51], null));
        _store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        _provisioner = new PoolProvisioner(_registry, _store, _resolver, NullLogger<PoolProvisioner>.Instance);
    }

    private PoolBootstrapService Bootstrap(params PoolDefinition[] configured) =>
        new(_registry, _store, _resolver, TestData.Options(o => { foreach (var p in configured) { o.Pools.Add(p); } }), NullLogger<PoolBootstrapService>.Instance);

    [Fact]
    public async Task GIVEN_AddAsync_WHEN_PoolIsValid_THEN_PoolIsRegisteredAndStored()
    {
        var pool = TestData.Pool();

        var result = await _provisioner.AddAsync(pool, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Same(pool, _registry.Find(pool.Name));
        await _store.Received(1).SaveAsync(pool, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_AddAsync_WHEN_DefinitionIsInvalid_THEN_FailsWithoutAskingNode()
    {
        var result = await _provisioner.AddAsync(TestData.Pool() with { Port = 0 }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("port", result.Error);
        await _resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
    }

    [Fact]
    public async Task GIVEN_AddAsync_WHEN_NodeRejectsFeeAddress_THEN_FailsWithNodeReason()
    {
        _resolver.ResolveAsync("bad", Arg.Any<CancellationToken>()).Returns(new AddressResolution(false, null, "Invalid checksum"));

        var result = await _provisioner.AddAsync(TestData.Pool() with { FeeAddress = "bad" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid checksum", result.Error);
        Assert.Empty(_registry.Pools);
    }

    [Fact]
    public async Task GIVEN_AddAsync_WHEN_PortConflicts_THEN_FailsWithoutStoring()
    {
        await _provisioner.AddAsync(TestData.Pool("a"), CancellationToken.None);
        _store.ClearReceivedCalls();

        var result = await _provisioner.AddAsync(TestData.Pool("b"), CancellationToken.None);

        Assert.False(result.Succeeded);
        await _store.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task GIVEN_RemoveAsync_WHEN_PoolExists_THEN_PoolIsUnregisteredAndDeleted()
    {
        await _provisioner.AddAsync(TestData.Pool("a"), CancellationToken.None);

        var removed = await _provisioner.RemoveAsync("a", CancellationToken.None);

        Assert.True(removed);
        Assert.Empty(_registry.Pools);
        await _store.Received(1).DeleteAsync("a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GIVEN_RemoveAsync_WHEN_PoolIsUnknown_THEN_ReturnsFalseWithoutTouchingStore()
    {
        Assert.False(await _provisioner.RemoveAsync("missing", CancellationToken.None));
        await _store.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task GIVEN_StartAsync_WHEN_ConfiguredAndStoredPoolsExist_THEN_BothAreRegistered()
    {
        _store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([TestData.Pool("stored", port: 2)]);

        await Bootstrap(TestData.Pool("configured", port: 1)).StartAsync(CancellationToken.None);

        Assert.Equal(["configured", "stored"], _registry.Pools.Select(p => p.Name));
    }

    [Fact]
    public async Task GIVEN_StartAsync_WHEN_PoolIsDisabled_THEN_ItIsNotRegistered()
    {
        await Bootstrap(TestData.Pool("off") with { Enabled = false }).StartAsync(CancellationToken.None);

        Assert.Empty(_registry.Pools);
    }

    [Fact]
    public async Task GIVEN_StartAsync_WHEN_StoredPoolConflictsWithConfiguredPool_THEN_ConfiguredPoolWins()
    {
        _store.GetAllAsync(Arg.Any<CancellationToken>()).Returns([TestData.Pool("stored", port: 1)]);

        await Bootstrap(TestData.Pool("configured", port: 1)).StartAsync(CancellationToken.None);

        Assert.Equal("configured", Assert.Single(_registry.Pools).Name);
    }

    [Fact]
    public async Task GIVEN_StopAsync_WHEN_Called_THEN_CompletesWithoutChangingPools()
    {
        var service = Bootstrap(TestData.Pool("a"));
        await service.StartAsync(CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        Assert.Single(_registry.Pools);
    }

    [Fact]
    public async Task GIVEN_StartAsync_WHEN_NodeRejectsAPoolsFeeAddress_THEN_ThatPoolIsRefusedAndOthersStart()
    {
        _resolver.ResolveAsync("wrong-network", Arg.Any<CancellationToken>()).Returns(new AddressResolution(false, null, "Invalid prefix"));

        await Bootstrap(TestData.Pool("bad", port: 1) with { FeeAddress = "wrong-network" }, TestData.Pool("good", port: 2))
            .StartAsync(CancellationToken.None);

        Assert.Equal("good", Assert.Single(_registry.Pools).Name);
    }
}