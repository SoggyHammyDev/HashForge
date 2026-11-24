using System.Globalization;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;

namespace HashForge.Mining.Jobs;

public static class BlockTemplateExtensions
{
    /// <summary>The template's target in the compact form block headers carry.</summary>
    public static uint CompactBits(this BlockTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return uint.Parse(template.Bits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>The difficulty a block built from this template must meet.</summary>
    public static double Difficulty(this BlockTemplate template) =>
        DifficultyMath.DifficultyFromTarget(DifficultyMath.TargetFromCompact(template.CompactBits()));
}