using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace HashForge.Domain;

/// <summary>
/// Decides what a miner may call one of their machines. A name can be written in any language, with
/// spaces, symbols and emoji; only characters that could not be shown, or that would let a name pass
/// itself off as something else, are refused.
/// </summary>
public static class WorkerNameFormat
{
    /// <summary>The longest name, counted as a reader would count it: "å" or a flag is one.</summary>
    public const int MaxLength = 32;

    /// <summary>
    /// The longest name as stored. One visible character can be built from many parts (a letter with a
    /// pile of accents), so the count above is not enough on its own to keep a name small.
    /// </summary>
    public const int MaxStoredLength = 128;

    // These two invisible characters are part of ordinary spelling in Persian and several Indian
    // scripts, and join the pieces of many emoji, so they are allowed where other invisible ones are not.
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
        // Line breaks and other control codes could forge lines in the pool's log.
        UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator => false,
        // Invisible formatting marks can reverse or hide text, making one name look like another.
        UnicodeCategory.Format => rune.Value is ZeroWidthNonJoiner or ZeroWidthJoiner,
        // Not characters at all: nothing can display them.
        UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned => false,
        _ => true,
    };
}
