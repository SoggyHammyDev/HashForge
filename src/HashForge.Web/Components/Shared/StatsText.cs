using HashForge.Domain.Formatting;
using HashForge.Mining.Statistics;

namespace HashForge.Web.Components.Shared;

/// <summary>Text for statistics that may not exist yet.</summary>
public static class StatsText
{
    public static string BestDifficulty(BestShare? best) => best is null ? Display.None : Display.Difficulty(best.Difficulty);

    public static string BestWorker(BestShare? best) => best is null ? Display.None : Display.Shorten(best.Worker);
}