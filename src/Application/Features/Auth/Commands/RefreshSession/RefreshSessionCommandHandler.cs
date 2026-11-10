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

        var session = sessionIssuer.IssueSessionInFamily(user, token.FamilyId);
        token.Revoke(now, session.RefreshTokenId);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            // A conflict (stale RowVersion) means another request rotated the same token first and
            // its response carries the new cookie: report a superseded token so the cookie is kept.
            return saved.Error.Type == ErrorType.Conflict
                ? Result.Failure<AuthTokens>(AuthErrors.RefreshSuperseded)
                : Result.Failure<AuthTokens>(saved.Error);
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

        // Reuse of a revoked token: treat its family (one login's chain) as stolen and revoke only that
        // family, so other devices stay signed in (ADR 0016). One set-based statement, so a concurrent
        // rotation cannot roll the revocation back (ADR 0014 decision 5).
        var revokedCount = await refreshTokens.RevokeActiveInFamilyAsync(token.FamilyId, now, cancellationToken);
        if (revokedCount > 0)
        {
            LogReuseDetected(logger, token.UserId, token.FamilyId, revokedCount);
        }
        else
        {
            // The family is already dead (logout, role change, earlier reuse): nothing to protect.
            LogRevokedTokenPresented(logger, token.UserId, token.FamilyId);
        }

        return Invalid();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; {RevokedCount} active refresh tokens of family {FamilyId} were revoked.")]
    private static partial void LogReuseDetected(ILogger logger, Guid userId, Guid familyId, int revokedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "A revoked refresh token of user {UserId} was presented; family {FamilyId} has no active tokens.")]
    private static partial void LogRevokedTokenPresented(ILogger logger, Guid userId, Guid familyId);
}
