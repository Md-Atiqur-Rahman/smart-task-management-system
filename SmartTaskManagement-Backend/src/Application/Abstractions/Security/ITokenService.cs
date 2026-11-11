using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>A signed access token and the instant it expires.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Issues short-lived signed access tokens.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, Role role, int tokenVersion);
}
