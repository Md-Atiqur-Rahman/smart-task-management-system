using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
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
/// Likewise the Project Manager demotion guard is per request: an owner change and a demotion of the
/// new owner at the same moment can both pass; the project is then owned by a non-Project-Manager
/// until an Admin changes the owner (design section 6). Documented, not prevented.
/// </remarks>
internal sealed class ChangeUserRoleCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenVersionStore tokenVersions,
    IProjectRepository projects,
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

        if (user.Role == Role.ProjectManager
            && command.Role != Role.ProjectManager
            && await projects.AnyOwnedByAsync(user.Id, cancellationToken))
        {
            return Result.Failure(UserErrors.OwnsProjects);
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
