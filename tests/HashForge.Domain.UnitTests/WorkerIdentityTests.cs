namespace HashForge.Domain.UnitTests;

public sealed class WorkerIdentityTests
{
    [Fact]
    public void GIVEN_TryParse_WHEN_UsernameHasAddressAndWorker_THEN_BothPartsAreReturned()
    {
        var parsed = WorkerIdentity.TryParse("bc1qaddress0000.rig1", out var identity);

        Assert.True(parsed);
        Assert.Equal(new WorkerIdentity("bc1qaddress0000", "rig1"), identity);
    }

    [Theory]
    [InlineData("bc1qaddress0000")]
    [InlineData("bc1qaddress0000.")]
    [InlineData("  bc1qaddress0000  ")]
    public void GIVEN_TryParse_WHEN_WorkerNameIsMissing_THEN_DefaultWorkerNameIsUsed(string username)
    {
        var parsed = WorkerIdentity.TryParse(username, out var identity);

        Assert.True(parsed);
        Assert.Equal("bc1qaddress0000", identity!.Address);
        Assert.Equal(WorkerIdentity.DefaultWorkerName, identity.WorkerName);
    }

    [Fact]
    public void GIVEN_TryParse_WHEN_WorkerNameContainsDots_THEN_OnlyFirstDotSplits()
    {
        Assert.True(WorkerIdentity.TryParse("bc1qaddress0000.rack.1", out var identity));

        Assert.Equal("rack.1", identity!.WorkerName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".rig1")]
    [InlineData("short.rig1")]
    [InlineData("not an address at all.rig1")]
    [InlineData("bc1qaddress0000.rig one")]
    [InlineData("bc1qaddress0000.rig\nINFO forged log line")]
    [InlineData("bc1qaddress0000.<b>x</b>")]
    [InlineData("bc1qaddress0000.a-worker-name-that-is-much-longer-than-allowed")]
    public void GIVEN_TryParse_WHEN_AddressIsMissing_THEN_ReturnsFalse(string? username)
    {
        var parsed = WorkerIdentity.TryParse(username, out var identity);

        Assert.False(parsed);
        Assert.Null(identity);
    }

    [Fact]
    public void GIVEN_ToString_WHEN_Called_THEN_ReturnsAddressDotWorker()
    {
        var identity = new WorkerIdentity("addr", "rig1");

        Assert.Equal("addr.rig1", identity.ToString());
        Assert.Equal("addr.rig1", identity.FullName);
    }
}

