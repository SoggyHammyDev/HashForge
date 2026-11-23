using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.UnitTests.Sessions;

public sealed class VarDiffControllerTests
{
    private static readonly DateTimeOffset Start = StratumTestKit.Now;

    private static VarDiffController Controller(Action<StratumOptions>? configure = null) =>
        new(StratumTestKit.Options(o =>
        {
            o.InitialDifficulty = 1000;
            o.MinDifficulty = 100;
            o.MaxDifficulty = 10_000;
            o.TargetSharesPerMinute = 12;
            o.RetargetSeconds = 60;
            o.VariancePercent = 30;
            configure?.Invoke(o);
        }).Value, Start);

    private static void Record(VarDiffController controller, int shares)
    {
        for (var i = 0; i < shares; i++)
        {
            controller.RecordShare();
        }
    }

    [Fact]
    public void GIVEN_Difficulty_WHEN_NewlyCreated_THEN_IsInitialDifficultyWithNoPrevious()
    {
        var controller = Controller();

        Assert.Equal(1000, controller.Difficulty);
        Assert.Null(controller.PreviousDifficulty);
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_WindowHasNotElapsed_THEN_ReturnsNull()
    {
        var controller = Controller();
        Record(controller, 5);

        Assert.Null(controller.TryRetarget(Start.AddSeconds(30)));
        Assert.Null(controller.TryRetarget(Start));
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_ShareRateIsOnTarget_THEN_DifficultyIsUnchanged()
    {
        var controller = Controller();
        Record(controller, 13);

        Assert.Null(controller.TryRetarget(Start.AddSeconds(60)));
        Assert.Equal(1000, controller.Difficulty);
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_SharesArriveTwiceAsFast_THEN_DifficultyDoublesAndPreviousIsKept()
    {
        var controller = Controller();
        Record(controller, 23);

        // 23 shares stays under the flood threshold of 24, so this is a normal end-of-window retarget.
        var retargeted = controller.TryRetarget(Start.AddSeconds(60));

        Assert.Equal(1000 * 23 / 12d, retargeted!.Value, precision: 6);
        Assert.Equal(retargeted, controller.Difficulty);
        Assert.Equal(1000, controller.PreviousDifficulty);
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_SharesArriveSlowly_THEN_DifficultyDrops()
    {
        var controller = Controller();
        Record(controller, 6);

        Assert.Equal(500, controller.TryRetarget(Start.AddSeconds(60)));
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_NoSharesArrive_THEN_DifficultyDropsByMaximumStep()
    {
        var controller = Controller();

        Assert.Equal(250, controller.TryRetarget(Start.AddSeconds(60)));
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_MinerFloodsShares_THEN_RetargetsEarlyByAtMostMaximumStep()
    {
        var controller = Controller();
        Record(controller, 24);

        Assert.Equal(4000, controller.TryRetarget(Start.AddSeconds(5)));
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_ResultWouldExceedLimits_THEN_ItIsClamped()
    {
        var high = Controller(o => o.InitialDifficulty = 9000);
        Record(high, 100);
        var low = Controller(o => o.InitialDifficulty = 150);

        Assert.Equal(10_000, high.TryRetarget(Start.AddSeconds(60)));
        Assert.Equal(100, low.TryRetarget(Start.AddSeconds(60)));
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_AlreadyAtLimit_THEN_ReturnsNull()
    {
        var controller = Controller(o => o.InitialDifficulty = 100);

        Assert.Null(controller.TryRetarget(Start.AddSeconds(60)));
    }

    [Fact]
    public void GIVEN_TryRetarget_WHEN_WindowCompletes_THEN_NextWindowStartsFresh()
    {
        var controller = Controller();
        Record(controller, 13);
        controller.TryRetarget(Start.AddSeconds(60));
        Record(controller, 6);

        Assert.Null(controller.TryRetarget(Start.AddSeconds(90)));
        Assert.Equal(500, controller.TryRetarget(Start.AddSeconds(120)));
    }

    [Theory]
    [InlineData(2000, 2000)]
    [InlineData(1, 100)]
    [InlineData(1e9, 10_000)]
    public void GIVEN_SetDifficulty_WHEN_MinerSuggestsValue_THEN_ItIsAppliedWithinLimits(double requested, double expected)
    {
        var controller = Controller();

        var applied = controller.SetDifficulty(requested, Start);

        Assert.Equal(expected, applied);
        Assert.Equal(expected, controller.Difficulty);
        Assert.Equal(1000, controller.PreviousDifficulty);
    }

    [Fact]
    public void GIVEN_SetDifficulty_WHEN_ValueIsUnchanged_THEN_PreviousDifficultyStaysNull()
    {
        var controller = Controller();

        controller.SetDifficulty(1000, Start);

        Assert.Null(controller.PreviousDifficulty);
    }
}
