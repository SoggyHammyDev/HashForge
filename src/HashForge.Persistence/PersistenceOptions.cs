using System.ComponentModel.DataAnnotations;

namespace HashForge.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    /// <summary>StackExchange.Redis connection string, e.g. <c>redis:6379</c>.</summary>
    [Required]
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>Prefix for every Redis key, so several pools can share one Redis.</summary>
    [Required]
    public string RedisKeyPrefix { get; set; } = "hashforge:";

    [Required]
    public string PostgresConnectionString { get; set; } = string.Empty;
}
