using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>Refresh token settings, bound from the "RefreshToken" section and validated on start.</summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    [Range(1, 90)]
    public int LifetimeDays { get; init; } = 7;

    [Range(0, 60)]
    public int ReuseGraceSeconds { get; init; } = 30;
}
