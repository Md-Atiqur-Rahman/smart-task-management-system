using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Commands.ChangeUserRole;

/// <summary>
/// Redis first, then the database (design section 5, ADR 0014): the forward-only version write
/// happens before the one save, so a failure can never leave old tokens working.
/// </summary>
/// <remarks>
/// Known limitation: the last-Admin check is per request; two Admins demoting each other at the
/// same moment can both pass it. Recovery: restart with a different AdminSeed:Email (design section 8).
/// </remarks>
internal sealed class ChangeUserRoleCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenVersionStore tokenVersions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ChangeUserRoleCommand>
{
    public async Task<Result> Handle(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.Role == Role.Admin
            && command.Role != Role.Admin
            && await users.CountByRoleAsync(Role.Admin, cancellationToken) == 1)
        {
            return Result.Failure(UserErrors.LastAdmin);
        }

        if (!user.ChangeRole(command.Role))
        {
            return Result.Success();
        }

        try
        {
            await tokenVersions.SetIfHigherAsync(user.Id, user.TokenVersion, cancellationToken);
        }
        catch (SharedStateUnavailableException)
        {
            return Result.Failure(AuthErrors.Unavailable);
        }

        var now = timeProvider.GetUtcNow();
        foreach (var token in await refreshTokens.ListActiveForUserAsync(user.Id, now, cancellationToken))
        {
            token.Revoke(now, replacedById: null);
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
