using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;

/// <summary>
/// Redis settings, bound from the "Redis" section. The connection string is a secret
/// (user-secrets or the Redis__ConnectionString environment variable), never in appsettings.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// StackExchange.Redis connection string. Without "ssl=true" the connection is not
    /// encrypted, which is allowed ONLY in Development and Testing (ADR 0011, ADR 0013).
    /// </summary>
    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Fixed lifetime of a token version entry, counted from its last write.</summary>
    [Range(1, 10080)]
    public int TokenVersionTtlMinutes { get; init; } = 60;

    [Range(100, 60000)]
    public int ConnectTimeoutMs { get; init; } = 2000;

    [Range(50, 60000)]
    public int OperationTimeoutMs { get; init; } = 1000;
}
