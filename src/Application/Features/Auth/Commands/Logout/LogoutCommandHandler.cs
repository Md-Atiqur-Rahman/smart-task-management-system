using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator generator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow();
        var token = await refreshTokens.GetByHashAsync(generator.Hash(command.RefreshToken), cancellationToken);
        if (token is null || token.RevokedAt is not null)
        {
            return Result.Success();
        }

        var revoked = token.Revoke(now, replacedById: null);
        if (revoked.IsFailure)
        {
            return Result.Success();
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
