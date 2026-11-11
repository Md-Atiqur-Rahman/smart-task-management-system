using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.ChangeProjectOwner;

/// <summary>The AdminOnly policy enforces the caller's role; the handler checks the new owner.</summary>
internal sealed class ChangeProjectOwnerCommandHandler(
    ISecurityContext securityContext,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAssignmentNotifier notifier,
    LiveEventBuffer liveEvents)
    : ICommandHandler<ChangeProjectOwnerCommand>
{
    public async Task<Result> Handle(ChangeProjectOwnerCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is null)
        {
            return Result.Failure(AuthErrors.TokenRevoked);
        }

        var project = await projects.GetByIdAsync(command.Id, cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        var owner = await users.GetByIdAsync(command.OwnerId, cancellationToken);
        if (owner is null || owner.Role != Role.ProjectManager)
        {
            return Result.Failure(ProjectErrors.InvalidOwner);
        }

        var previousOwnerId = project.OwnerId;
        if (!project.ChangeOwner(owner.Id))
        {
            return Result.Success();
        }

        await notifier.ProjectOwnerChangedAsync(project, previousOwnerId, callerId, cancellationToken);

        var result = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            // Not the request token: a request aborted right after a successful save still publishes.
            await liveEvents.FlushAsync(CancellationToken.None);
        }

        return result;
    }
}
