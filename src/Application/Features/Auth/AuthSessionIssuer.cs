using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>
/// Creates the tokens of a new session: adds a RefreshToken (hash only) and builds the access token.
/// The caller saves. Never logs tokens.
/// </summary>
internal sealed partial class AuthSessionIssuer(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator generator,
    ITokenService tokenService,
    ITokenVersionStore tokenVersionStore,
    TimeProvider timeProvider,
    IOptions<RefreshTokenOptions> options,
    ILogger<AuthSessionIssuer> logger)
{
    public AuthTokens Issue(User user) => IssueSession(user).Tokens;

    /// <summary>Like <see cref="Issue"/>, also returns the id of the new refresh token (needed by rotation).</summary>
    public IssuedSession IssueSession(User user)
    {
        var now = timeProvider.GetUtcNow();
        var generated = generator.Generate();
        var refreshToken = RefreshToken.Issue(
            user.Id,
            generated.TokenHash,
            now,
            TimeSpan.FromDays(options.Value.LifetimeDays));
        refreshTokens.Add(refreshToken);

        var accessToken = tokenService.CreateAccessToken(user.Id, user.Role, user.TokenVersion);
        var dto = new CurrentUserDto(user.Id, user.FullName, user.Email.Value, user.Role);
        return new IssuedSession(new AuthTokens(accessToken, generated.RawToken, refreshToken.ExpiresAt, dto), refreshToken.Id);
    }

    /// <summary>Publishes the token version to the shared store; a store outage never fails the request.</summary>
    public async Task PublishTokenVersionAsync(User user, CancellationToken ct)
    {
        try
        {
            await tokenVersionStore.SetIfHigherAsync(user.Id, user.TokenVersion, ct);
        }
        catch (SharedStateUnavailableException exception)
        {
            LogPublishFailed(logger, exception, user.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token version of user {UserId} was not published because the shared state store is unavailable.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, Guid userId);
}

/// <summary>The tokens of a new session and the id of its refresh token.</summary>
internal sealed record IssuedSession(AuthTokens Tokens, Guid RefreshTokenId);
