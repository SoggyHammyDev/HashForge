using HashForge.Mining.Jobs;
using HashForge.Mining.Network;

namespace HashForge.Mining.UnitTests.Network;

public sealed class NetworkDifficultyTests
{
    private readonly ITemplateRefresher _templates = Substitute.For<ITemplateRefresher>();
    private readonly NetworkStatsStore _stats = new();
    private readonly NetworkDifficulty _difficulty;

    public NetworkDifficultyTests()
    {
        MineAt(null);
        _difficulty = new NetworkDifficulty(_templates, _stats);
    }

    private void MineAt(double? difficulty) =>
        _templates.Status.Returns(new TemplateStatus("00ab", TestData.Now) { Difficulty = difficulty });

    private void TipAt(double difficulty) =>
        _stats.Update(new NetworkStats("test", 1, 1, difficulty, 0, "00", false, 1, true, 0, TestData.Now));

    [Fact]
    public void GIVEN_Current_WHEN_NothingIsKnownYet_THEN_IsZero()
    {
        Assert.Equal(0, _difficulty.Current);
    }

    [Fact]
    public void GIVEN_Current_WHEN_NoTemplateYet_THEN_FallsBackToTheChainTip()
    {
        TipAt(5000);

        Assert.Equal(5000, _difficulty.Current);
    }

    [Fact]
    public void GIVEN_Current_WHEN_TemplateDiffersFromChainTip_THEN_TheBlockBeingMinedWins()
    {
        TipAt(1);
        MineAt(236_000_000);

        Assert.Equal(236_000_000, _difficulty.Current);
    }

    [Fact]
    public void GIVEN_Current_WHEN_TestNetworkOffersAnEasyBlock_THEN_RegularDifficultyIsKept()
    {
        MineAt(236_000_000);
        _ = _difficulty.Current;

        MineAt(1);

        Assert.Equal(236_000_000, _difficulty.Current);
    }

    [Fact]
    public void GIVEN_Current_WHEN_OnlyAnEasyBlockHasEverBeenSeen_THEN_ReportsIt()
    {
        MineAt(1);

        Assert.Equal(1, _difficulty.Current);
    }

    [Fact]
    public void GIVEN_Current_WHEN_NetworkIsEasierThanOne_THEN_ReportsTheTemplateDifficulty()
    {
        TipAt(123);
        MineAt(4.6e-10);

        Assert.Equal(4.6e-10, _difficulty.Current);
    }

    [Fact]
    public void GIVEN_Difficulty_WHEN_TemplateHasMinimumBits_THEN_IsOne()
    {
        Assert.Equal(1, TestData.Template().Difficulty(), 6);
        Assert.Equal(0x1d00ffffu, TestData.Template().CompactBits());
    }
}