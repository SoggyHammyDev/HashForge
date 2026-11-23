using HashForge.Mining.Shares;

namespace HashForge.Mining.Notifications;

public sealed record BlockFoundNotification(FoundBlock Block) : PoolNotification;
