using HashForge.Domain;
using HashForge.Mining.Shares;

namespace HashForge.Mining.UnitTests.Shares;

public sealed class FoundBlockTests
{
    private static FoundBlock Block(int payouts, int minersPaid) => new(
        "prop",
        PayoutScheme.Prop,
        850_000,
        new string('a', 64),
        new WorkerIdentity("bc1qfinder", "rig1"),
        312_500_000,
        [.. Enumerable.Range(0, payouts).Select(i => new PayoutEntry($"bc1q{i}", 1_000))],
        50,
        DateTimeOffset.UnixEpoch,
        true,
        null)
    {
        MinersPaid = minersPaid,
    };

    [Fact]
    public void GIVEN_IsPoolFee_WHEN_OneMoreOutputThanMinersPaid_THEN_OnlyTheLastOutputIsTheFee()
    {
        var block = Block(payouts: 3, minersPaid: 2);

        Assert.False(block.IsPoolFee(0));
        Assert.False(block.IsPoolFee(1));
        Assert.True(block.IsPoolFee(2));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 34)]
    public void GIVEN_IsPoolFee_WHEN_OutputsDoNotLeaveExactlyOneForTheFee_THEN_NoOutputIsTheFee(int payouts, int minersPaid)
    {
        var block = Block(payouts, minersPaid);

        Assert.DoesNotContain(Enumerable.Range(0, payouts), block.IsPoolFee);
    }
}
