using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Api.Options;

/// <summary>Fixed-window limit per client IP, bound from "RateLimiting".</summary>
internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int PermitLimit { get; init; } = 100;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}
