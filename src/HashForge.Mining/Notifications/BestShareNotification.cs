using HashForge.Mining.Statistics;

namespace HashForge.Mining.Notifications;

public sealed record BestShareNotification(string PoolName, BestShare Share, double NetworkDifficulty) : PoolNotification;
