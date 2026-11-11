using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Api.Options;

/// <summary>
/// Fixed-window limits per client IP, bound from "RateLimiting": the global limit for every
/// endpoint and the stricter auth limit for login and register (ADR 0013).
/// </summary>
internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int PermitLimit { get; init; } = 100;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    [Range(1, 100_000)]
    public int AuthPermitLimit { get; init; } = 10;

    [Range(1, 3600)]
    public int AuthWindowSeconds { get; init; } = 60;
}
