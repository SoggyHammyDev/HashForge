using HashForge.Mining.History;

namespace HashForge.Web.Components.Shared;

public sealed record ChartPoint(double X, double Y, PoolHistorySample Sample);
