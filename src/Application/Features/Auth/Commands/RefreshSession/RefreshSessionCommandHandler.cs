using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RefreshSession;

internal sealed partial class RefreshSessionCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator generator,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    AuthSessionIssuer sessionIssuer,
    TimeProvider timeProvider,
    IOptions<RefreshTokenOptions> options,
    ILogger<RefreshSessionCommandHandler> logger)
    : ICommandHandler<RefreshSessionCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var token = await refreshTokens.GetByHashAsync(generator.Hash(command.RefreshToken), cancellationToken);
        if (token is null || now >= token.ExpiresAt)
        {
            return Invalid();
        }

        if (token.RevokedAt is not null)
        {
            return await HandleRevokedAsync(token, now, cancellationToken);
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return Invalid();
        }

        var session = sessionIssuer.IssueSession(user);
        var revoked = token.Revoke(now, session.RefreshTokenId);
        if (revoked.IsFailure)
        {
            return Invalid();
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            // A stale RowVersion means another request rotated the same token first.
            return Invalid();
        }

        await sessionIssuer.PublishTokenVersionAsync(user, cancellationToken);
        return Result.Success(session.Tokens);
    }

    private static Result<AuthTokens> Invalid() => Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);

    private async Task<Result<AuthTokens>> HandleRevokedAsync(RefreshToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var grace = TimeSpan.FromSeconds(options.Value.ReuseGraceSeconds);
        if (token.IsWithinRotationGrace(now, grace))
        {
            return Result.Failure<AuthTokens>(AuthErrors.RefreshSuperseded);
        }

        // Reuse of a revoked token: treat the family as stolen and revoke every active token of the user.
        var active = await refreshTokens.ListActiveForUserAsync(token.UserId, now, cancellationToken);
        foreach (var other in active)
        {
            other.Revoke(now, replacedById: null);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogReuseDetected(logger, token.UserId);
        return Invalid();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; all active refresh tokens were revoked.")]
    private static partial void LogReuseDetected(ILogger logger, Guid userId);
}
