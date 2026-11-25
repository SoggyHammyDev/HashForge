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
    [InlineData("rig one")]
    [InlineData("rig→one")]
    [InlineData("rig⇒")]
    [InlineData("garage🔥")]
    [InlineData("<b>x</b>")]
    [InlineData("rig!")]
    [InlineData("rig@home")]
    [InlineData("rig one")]
    [InlineData("rig\tone")]
    [InlineData("rig\0")]
    [InlineData("rig\u2028one")]
    [InlineData("rig\u202Egnp.exe")]
    [InlineData("rig\u200Bone")]
    [InlineData("rig\uFEFF")]
    [InlineData("rig\uE000")]
    public void GIVEN_IsAcceptable_WHEN_NameHasAnythingOtherThanLettersDigitsAndJoiningPunctuation_THEN_ItIsRefused(string? name)
    {
        Assert.False(WorkerNameFormat.IsAcceptable(name));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_NameHasHalfOfATwoPartCharacter_THEN_ItIsRefused()
    {
        // An old Chinese character outside the common range is stored as two parts.
        const string TwoPartLetter = "\uD840\uDC0B";
        var halfALetter = "rig" + TwoPartLetter[0];

        Assert.True(WorkerNameFormat.IsAcceptable("rig" + TwoPartLetter));
        Assert.False(WorkerNameFormat.IsAcceptable(halfALetter));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_NameIsAtTheLimit_THEN_EachVisibleCharacterCountsOnce()
    {
        var thirtyTwoAccentedLetters = string.Concat(Enumerable.Repeat("a\u030A", WorkerNameFormat.MaxLength));
        var thirtyThreeLetters = new string('å', WorkerNameFormat.MaxLength + 1);

        Assert.True(WorkerNameFormat.IsAcceptable(thirtyTwoAccentedLetters));
        Assert.False(WorkerNameFormat.IsAcceptable(thirtyThreeLetters));
    }

    [Fact]
    public void GIVEN_IsAcceptable_WHEN_FewVisibleCharactersCarryAPileOfAccents_THEN_ItIsRefusedForItsStoredSize()
    {
        var oneLetterManyAccents = "a" + new string('\u0301', WorkerNameFormat.MaxStoredLength);

        Assert.False(WorkerNameFormat.IsAcceptable(oneLetterManyAccents));
    }
}
