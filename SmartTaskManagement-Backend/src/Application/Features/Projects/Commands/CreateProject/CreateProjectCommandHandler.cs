using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.CreateProject;

/// <summary>Write path: owner rules by caller role -> aggregate factory -> write repository -> UnitOfWork.</summary>
internal sealed class CreateProjectCommandHandler(
    ISecurityContext securityContext,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAssignmentNotifier notifier,
    LiveEventBuffer liveEvents)
    : ICommandHandler<CreateProjectCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<Guid>(AuthErrors.TokenRevoked);
        }

        Guid ownerId;
        if (callerRole == Role.Admin)
        {
            if (command.OwnerId is not { } requestedOwnerId)
            {
                return Result.Failure<Guid>(ProjectErrors.InvalidOwner);
            }

            var owner = await users.GetByIdAsync(requestedOwnerId, cancellationToken);
            if (owner is null || owner.Role != Role.ProjectManager)
            {
                return Result.Failure<Guid>(ProjectErrors.InvalidOwner);
            }

            ownerId = owner.Id;
        }
        else
        {
            if (command.OwnerId is { } requestedOwnerId && requestedOwnerId != callerId)
            {
                return Result.Failure<Guid>(ProjectErrors.OwnerNotAllowed);
            }

            ownerId = callerId;
        }

        var project = Project.Create(command.Name, command.Description, ownerId);
        projects.Add(project);
        await notifier.ProjectOwnerChangedAsync(project, null, callerId, cancellationToken);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<Guid>(saved.Error);
        }

        // Not the request token: a request aborted right after a successful save still publishes.
        await liveEvents.FlushAsync(CancellationToken.None);
        return Result.Success(project.Id);
    }
}
