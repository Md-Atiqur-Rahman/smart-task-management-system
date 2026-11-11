using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>Result of a successful register, login or refresh. The raw refresh token is shown to the client once.</summary>
public sealed record AuthTokens(
    AccessToken AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserDto User);
