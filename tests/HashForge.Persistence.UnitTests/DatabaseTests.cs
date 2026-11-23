using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Shares;
using HashForge.Persistence.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HashForge.Persistence.UnitTests;

/// <summary>Exercises the repositories against an in-memory relational database.</summary>
public sealed class DatabaseTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly TestContextFactory _factory;

    public DatabaseTests()
    {
        _connection.Open();
        _factory = new TestContextFactory(new DbContextOptionsBuilder<HashForgeDbContext>().UseSqlite(_connection).Options);
        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class TestContextFactory(DbContextOptions<HashForgeDbContext> options) : IDbContextFactory<HashForgeDbContext>
    {
        public HashForgeDbContext CreateDbContext() => new(options);
    }

    private static FoundBlock Block(int height, DateTimeOffset foundAt, bool accepted = true) => new(
        "pplns",
        PayoutScheme.Pplns,
        height,
        $"hash-{height}",
        new WorkerIdentity("bc1qfinder", "rig1"),
        312_500_000,
        [new PayoutEntry("bc1qa", 200_000_000), new PayoutEntry("bc1qb", 112_500_000)],
        87.5,
        foundAt,
        accepted,
        accepted ? null : "high-hash")
    {
        MinersPaid = 2,
    };

    private static PoolDefinition Pool(string name = "prop", int port = 3335) => new()
    {
        Name = name,
        Scheme = PayoutScheme.Prop,
        Port = port,
        FeeAddress = "bc1qfee",
        FeePercent = 1.25m,
        PplnsWindowMultiplier = 3,
        FinderBonusSatoshis = 50_000_000,
        MaxCoinbaseOutputs = 100,
        MinimumPayoutSatoshis = 5_000,
    };

    [Fact]
    public async Task GIVEN_BlockAddAsync_WHEN_BlockIsStored_THEN_GetRecentReturnsItWithPayoutsInOrder()
    {
        var repository = new EfBlockRepository(_factory);
        var block = Block(850_000, Now);

        await repository.AddAsync(block, CancellationToken.None);
        var stored = Assert.Single(await repository.GetRecentAsync(10, CancellationToken.None));

        Assert.Equal(block.Payouts, stored.Payouts);
        Assert.Equal(2, stored.MinersPaid);
        Assert.Equal(block with { Payouts = stored.Payouts }, stored);
    }

    [Fact]
    public async Task GIVEN_BlockAddAsync_WHEN_BlockWasRejected_THEN_RejectReasonIsKept()
    {
        var repository = new EfBlockRepository(_factory);

        await repository.AddAsync(Block(1, Now, accepted: false), CancellationToken.None);
        var stored = Assert.Single(await repository.GetRecentAsync(10, CancellationToken.None));

        Assert.False(stored.Accepted);
        Assert.Equal("high-hash", stored.RejectReason);
    }

    [Fact]
    public async Task GIVEN_GetRecentAsync_WHEN_MoreBlocksThanRequested_THEN_ReturnsNewestFirstUpToCount()
    {
        var repository = new EfBlockRepository(_factory);
        await repository.AddAsync(Block(1, Now), CancellationToken.None);
        await repository.AddAsync(Block(3, Now.AddHours(2)), CancellationToken.None);
        await repository.AddAsync(Block(2, Now.AddHours(1)), CancellationToken.None);

        var recent = await repository.GetRecentAsync(2, CancellationToken.None);

        Assert.Equal([3, 2], recent.Select(b => b.Height));
    }

    [Fact]
    public async Task GIVEN_HistoryGetSinceAsync_WHEN_SamplesSpanPoolsAndTimes_THEN_ReturnsMatchingSamplesOldestFirst()
    {
        var repository = new EfPoolHistoryRepository(_factory);
        var kept1 = new PoolHistorySample("solo", Now.AddMinutes(5), 2e12, 3, 4);
        var kept2 = new PoolHistorySample("solo", Now.AddMinutes(10), 3e12, 5, 6);
        await repository.AddAsync(
            [kept2, new PoolHistorySample("solo", Now.AddMinutes(-5), 1e12, 1, 1), kept1, new PoolHistorySample("prop", Now.AddMinutes(5), 9e12, 9, 9)],
            CancellationToken.None);

        var samples = await repository.GetSinceAsync("solo", Now, CancellationToken.None);

        Assert.Equal([kept1, kept2], samples);
    }

    [Fact]
    public async Task GIVEN_PoolSaveAsync_WHEN_PoolIsNew_THEN_GetAllReturnsItUnchanged()
    {
        var store = new EfPoolDefinitionStore(_factory);
        var pool = Pool();

        await store.SaveAsync(pool, CancellationToken.None);

        Assert.Equal(pool, Assert.Single(await store.GetAllAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task GIVEN_PoolSaveAsync_WHEN_PoolAlreadyExists_THEN_ItIsUpdatedNotDuplicated()
    {
        var store = new EfPoolDefinitionStore(_factory);
        await store.SaveAsync(Pool(), CancellationToken.None);
        var changed = Pool() with { Port = 4444, FeePercent = 2m, FeeAddress = "bc1qother" };

        await store.SaveAsync(changed, CancellationToken.None);

        Assert.Equal(changed, Assert.Single(await store.GetAllAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task GIVEN_PoolGetAllAsync_WHEN_SeveralStored_THEN_OrderedByPort()
    {
        var store = new EfPoolDefinitionStore(_factory);
        await store.SaveAsync(Pool("b", 20), CancellationToken.None);
        await store.SaveAsync(Pool("a", 10), CancellationToken.None);

        Assert.Equal(["a", "b"], (await store.GetAllAsync(CancellationToken.None)).Select(p => p.Name));
    }

    [Fact]
    public async Task GIVEN_PoolDeleteAsync_WHEN_PoolExists_THEN_OnlyThatPoolIsRemoved()
    {
        var store = new EfPoolDefinitionStore(_factory);
        await store.SaveAsync(Pool("a", 10), CancellationToken.None);
        await store.SaveAsync(Pool("b", 20), CancellationToken.None);

        await store.DeleteAsync("a", CancellationToken.None);
        await store.DeleteAsync("missing", CancellationToken.None);

        Assert.Equal("b", Assert.Single(await store.GetAllAsync(CancellationToken.None)).Name);
    }

    [Fact]
    public async Task GIVEN_MigrationServiceStartAsync_WHEN_HostStarts_THEN_DatabaseIsMigratedOnce()
    {
        var migrator = Substitute.For<IDatabaseMigrator>();
        var service = new DatabaseMigrationService(migrator, NullLogger<DatabaseMigrationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        await migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GIVEN_DesignTimeFactory_WHEN_ContextCreated_THEN_ModelMatchesMigrationsWithNoPendingChanges()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains("InitialCreate", context.Database.GetMigrations().First());
        Assert.Equal(3, context.Database.GetMigrations().Count());
    }
}
