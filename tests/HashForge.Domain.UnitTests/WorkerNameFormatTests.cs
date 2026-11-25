namespace HashForge.Domain.UnitTests;

public sealed class WorkerNameFormatTests
{
    [Theory]
    [InlineData("rig1")]
    [InlineData("rack.1_a-b")]
    [InlineData("Gårdsrigg")]
    [InlineData("Kjøkken-æøå")]
    [InlineData("Größe")]
    [InlineData("Ферма")]
    [InlineData("矿机一号")]
    [InlineData("מכונה")]
    [InlineData("مزرعة")]
    [InlineData("खनिक")]
    [InlineData("rig one")]
    [InlineData("garage 🔥")]
    [InlineData("👨‍👩‍👧 🇸🇪")]
    [InlineData("<b>x</b>")]
    public void GIVEN_IsAcceptable_WHEN_NameIsWrittenInAnyLanguage_THEN_ItIsAccepted(string name)
    {
        Assert.True(WorkerNameFormat.IsAcceptable(name));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_NameUsesJoinersThatSpellingNeeds_THEN_ItIsAccepted()
    {
        // The Persian word for "I want" is written with a zero-width non-joiner.
        const string Persian = "می\u200Cخواهم";

        Assert.True(WorkerNameFormat.IsAcceptable(Persian));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("rig\nINFO forged log line")]
    [InlineData("rig\tone")]
    [InlineData("rig\0")]
    [InlineData("rig\u2028one")]
    [InlineData("rig\u202Egnp.exe")]
    [InlineData("rig\u200Bone")]
    [InlineData("rig\uFEFF")]
    [InlineData("rig\uE000")]
    public void GIVEN_IsAcceptable_WHEN_NameHasCharactersThatCannotBeShownOrCouldMislead_THEN_ItIsRefused(string? name)
    {
        Assert.False(WorkerNameFormat.IsAcceptable(name));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_NameHasHalfOfATwoPartCharacter_THEN_ItIsRefused()
    {
        var halfAnEmoji = "rig" + "🔥"[0];

        Assert.False(WorkerNameFormat.IsAcceptable(halfAnEmoji));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_NameIsAtTheLimit_THEN_EachVisibleCharacterCountsOnce()
    {
        var thirtyTwoFlags = string.Concat(Enumerable.Repeat("🇳🇴", WorkerNameFormat.MaxLength));
        var thirtyThreeLetters = new string('å', WorkerNameFormat.MaxLength + 1);

        Assert.True(WorkerNameFormat.IsAcceptable(thirtyTwoFlags));
        Assert.False(WorkerNameFormat.IsAcceptable(thirtyThreeLetters));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_FewVisibleCharactersCarryAPileOfAccents_THEN_ItIsRefusedForItsStoredSize()
    {
        var oneLetterManyAccents = "a" + new string('\u0301', WorkerNameFormat.MaxStoredLength);

        Assert.False(WorkerNameFormat.IsAcceptable(oneLetterManyAccents));
    }
}
