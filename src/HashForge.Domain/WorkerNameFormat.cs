using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace HashForge.Domain;

/// <summary>
/// Decides what a miner may call one of their machines. A name is made of letters and digits, which may
/// come from any language, joined by '.', '_' or '-'. Spaces, punctuation, symbols, arrows and emoji are
/// refused: they are not needed to write a name, and keeping them out leaves nothing that could forge a
/// log line or carry markup into a page or a message.
/// </summary>
public static class WorkerNameFormat
{
    /// <summary>The longest name, counted as a reader would count it: "å" is one, however it was typed.</summary>
    public const int MaxLength = 32;

    /// <summary>
    /// The longest name as stored. One visible character can be built from many parts (a letter with a
    /// pile of accents), so the count above is not enough on its own to keep a name small.
    /// </summary>
    public const int MaxStoredLength = 128;

    // These two invisible characters are part of ordinary spelling in Persian and several Indian
    // scripts, so they are allowed where no other invisible character is.
    private const int ZeroWidthNonJoiner = 0x200C;
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>
    /// Whether a miner may use this name. Names are compared exactly as sent: the pool is built without
    /// language data, so it cannot tell that two different spellings of one accented letter are the same.
    /// </summary>
    public static bool IsAcceptable([NotNullWhen(true)] string? name) =>
        !string.IsNullOrEmpty(name)
        && name.Length <= MaxStoredLength
        && HasOnlyAllowedCharacters(name)
        && new StringInfo(name).LengthInTextElements <= MaxLength;

    private static bool HasOnlyAllowedCharacters(string name)
    {
        for (var index = 0; index < name.Length;)
        {
            // Half of a two-part character on its own is not text, and cannot be stored or shown.
            if (!Rune.TryGetRuneAt(name, index, out var rune) || !IsAllowed(rune))
            {
                return false;
            }

            index += rune.Utf16SequenceLength;
        }

        return true;
    }

    private static bool IsAllowed(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter => true,
        // Accents and vowel signs that attach to the letter before them.
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark => true,
        // Digits of any script, and numerals that are letters, such as the Chinese zero.
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber => true,
        _ => rune.Value is '.' or '_' or '-' or ZeroWidthNonJoiner or ZeroWidthJoiner,
    };
}
