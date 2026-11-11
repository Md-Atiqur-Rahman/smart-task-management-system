using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ChangeTaskStatus;

/// <summary>A manager of the project or the assignee may change the status; anyone else gets not found.</summary>
internal sealed class ChangeTaskStatusCommandHandler(
    ISecurityContext securityContext,
    ITaskItemRepository tasks,
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    IAssignmentNotifier notifier,
    LiveEventBuffer liveEvents)
    : ICommandHandler<ChangeTaskStatusCommand>
{
    public async Task<Result> Handle(ChangeTaskStatusCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure(AuthErrors.TokenRevoked);
        }

        var task = await tasks.GetByIdAsync(command.Id, cancellationToken);
        if (task is null)
        {
            return Result.Failure(TaskErrors.NotFound);
        }

        var project = await projects.GetByIdAsync(task.ProjectId, cancellationToken);
        if (project is null || !TaskAccess.CanSee(task, project, callerId, callerRole))
        {
            return Result.Failure(TaskErrors.NotFound);
        }

        if (!task.ChangeStatus(command.Status))
        {
            return Result.Success();
        }

        await notifier.TaskChangedAsync(task, callerId, cancellationToken);

        var result = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            // Not the request token: a request aborted right after a successful save still publishes.
            await liveEvents.FlushAsync(CancellationToken.None);
        }

        return result;
    }
}
