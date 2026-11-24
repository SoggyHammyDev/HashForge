using System.Text.RegularExpressions;

namespace HashForge.Web.UnitTests;

/// <summary>
/// The reconnect dialog ships with Blazor's template colours, which are a white box whatever the theme.
/// These tests keep it on the site's own theme variables so it is readable in dark and light mode.
/// </summary>
public sealed partial class ReconnectModalStyleTests
{
    private static readonly string Modal = ReadStyles("ReconnectModal.razor.css");
    private static readonly string Theme = ReadStyles("app.css");

    [Theory]
    [InlineData("background-color", "var(--surface-raised)")]
    [InlineData("color", "var(--text)")]
    [InlineData("border", "1px solid var(--border-strong)")]
    public void GIVEN_ReconnectModal_WHEN_Styled_THEN_DialogTakesItsColoursFromTheTheme(string property, string value)
    {
        var dialog = Rule("#components-reconnect-modal");

        Assert.Contains($"    {property}: {value};", dialog, StringComparison.Ordinal);
    }

    [Fact]
    public void GIVEN_ReconnectModal_WHEN_Styled_THEN_NoColourIsFixedOutsideTheBackdrop()
    {
        var withoutBackdrop = Modal.Replace(Rule("#components-reconnect-modal::backdrop"), string.Empty, StringComparison.Ordinal);

        Assert.Empty(FixedColour().Matches(withoutBackdrop).Select(match => match.Value));
    }

    [Fact]
    public void GIVEN_ReconnectModal_WHEN_Styled_THEN_EveryColourVariableItUsesIsDefinedForDarkAndLight()
    {
        var used = VariableUse().Matches(Modal)
            .Select(match => match.Groups[1].Value)
            .Where(name => !name.StartsWith("--radius", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, name => Assert.Equal(2, VariableDefinition(name).Count(Theme)));
    }

    [Fact]
    public void GIVEN_ReconnectModal_WHEN_Styled_THEN_DialogItselfShowsNoFocusRing()
    {
        Assert.Contains("    outline: none;", Rule("#components-reconnect-modal"), StringComparison.Ordinal);
    }

    [Fact]
    public void GIVEN_ReconnectModal_WHEN_Styled_THEN_ButtonsKeepTheSiteWideButtonStyle()
    {
        Assert.DoesNotContain("#components-reconnect-modal button", Modal, StringComparison.Ordinal);
    }

    private static string ReadStyles(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Styles", fileName)).ReplaceLineEndings("\n");

    /// <summary>The whole rule, selector to closing brace, whose selector is exactly this.</summary>
    private static string Rule(string selector)
    {
        var start = Modal.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No rule for {selector}");
        return Modal[start..(Modal.IndexOf('}', start) + 1)];
    }

    private static Regex VariableDefinition(string name) =>
        new($@"^\s*{Regex.Escape(name)}\s*:", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(|:\s*(white|black)\b", RegexOptions.CultureInvariant)]
    private static partial Regex FixedColour();

    [GeneratedRegex(@"var\((--[a-z-]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex VariableUse();
}
