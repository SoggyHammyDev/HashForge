using HashForge.Domain;
using HashForge.Mining.Pools;

namespace HashForge.Mining.UnitTests.Pools;

public sealed class PoolRegistryTests
{
    private readonly PoolRegistry _registry = new();

    [Fact]
    public void GIVEN_TryAdd_WHEN_PoolIsValid_THEN_PoolIsListedAndEventRaised()
    {
        PoolDefinition? announced = null;
        _registry.PoolAdded += (_, args) => announced = args.Pool;
        var pool = TestData.Pool();

        var added = _registry.TryAdd(pool, out var error);

        Assert.True(added);
        Assert.Null(error);
        Assert.Same(pool, announced);
        Assert.Same(pool, Assert.Single(_registry.Pools));
    }

    [Fact]
    public void GIVEN_TryAdd_WHEN_PoolIsInvalid_THEN_ReturnsValidationError()
    {
        var added = _registry.TryAdd(TestData.Pool() with { Port = 0 }, out var error);

        Assert.False(added);
        Assert.Contains("port", error);
        Assert.Empty(_registry.Pools);
    }

    [Fact]
    public void GIVEN_TryAdd_WHEN_NameAlreadyExists_THEN_ReturnsConflictWithoutRaisingEvent()
    {
        _registry.TryAdd(TestData.Pool("solo", port: 1), out _);
        var raised = false;
        _registry.PoolAdded += (_, _) => raised = true;

        var added = _registry.TryAdd(TestData.Pool("SOLO".ToLowerInvariant(), port: 2), out var error);

        Assert.False(added);
        Assert.Contains("already exists", error);
        Assert.False(raised);
    }

    [Fact]
    public void GIVEN_TryAdd_WHEN_PortAlreadyUsed_THEN_ReturnsConflictNamingOtherPool()
    {
        _registry.TryAdd(TestData.Pool("solo", port: 3333), out _);

        var added = _registry.TryAdd(TestData.Pool("prop", port: 3333), out var error);

        Assert.False(added);
        Assert.Contains("'solo'", error);
    }

    [Fact]
    public void GIVEN_Pools_WHEN_SeveralRegistered_THEN_OrderedByPort()
    {
        _registry.TryAdd(TestData.Pool("b", port: 20), out _);
        _registry.TryAdd(TestData.Pool("a", port: 10), out _);

        Assert.Equal(["a", "b"], _registry.Pools.Select(p => p.Name));
    }

    [Fact]
    public void GIVEN_Find_WHEN_NameDiffersInCase_THEN_PoolIsFound()
    {
        _registry.TryAdd(TestData.Pool("solo"), out _);

        Assert.NotNull(_registry.Find("SOLO"));
        Assert.Null(_registry.Find("missing"));
    }

    [Fact]
    public void GIVEN_Remove_WHEN_PoolExists_THEN_PoolIsGoneAndEventRaised()
    {
        _registry.TryAdd(TestData.Pool("solo"), out _);
        PoolDefinition? announced = null;
        _registry.PoolRemoved += (_, args) => announced = args.Pool;

        var removed = _registry.Remove("solo");

        Assert.True(removed);
        Assert.Equal("solo", announced!.Name);
        Assert.Empty(_registry.Pools);
    }

    [Fact]
    public void GIVEN_Remove_WHEN_PoolDoesNotExist_THEN_ReturnsFalseWithoutEvent()
    {
        var raised = false;
        _registry.PoolRemoved += (_, _) => raised = true;

        Assert.False(_registry.Remove("missing"));
        Assert.False(raised);
    }
}
