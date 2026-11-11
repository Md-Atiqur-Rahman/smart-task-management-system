using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;

/// <summary>Only a manager of the project may assign; who may be assigned depends on the caller role.</summary>
internal sealed class AssignTaskCommandHandler(
    ISecurityContext securityContext,
    ITaskItemRepository tasks,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAssignmentNotifier notifier,
    LiveEventBuffer liveEvents)
    : ICommandHandler<AssignTaskCommand>
{
    public async Task<Result> Handle(AssignTaskCommand command, CancellationToken cancellationToken)
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
        if (project is null || !ProjectAccess.CanManage(project, callerId, callerRole))
        {
            return Result.Failure(TaskErrors.NotFound);
        }

        if (command.AssigneeId is { } assigneeId)
        {
            var assigneeError = await TaskAssigneeRules.CheckAsync(
                assigneeId, callerId, callerRole, users, cancellationToken);
            if (assigneeError is not null)
            {
                return Result.Failure(assigneeError);
            }
        }

        var previousAssigneeId = task.AssigneeId;
        if (!task.Assign(command.AssigneeId))
        {
            return Result.Success();
        }

        await notifier.TaskAssigneeChangedAsync(task, previousAssigneeId, callerId, cancellationToken);

        var result = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            // Not the request token: a request aborted right after a successful save still publishes.
            await liveEvents.FlushAsync(CancellationToken.None);
        }

        return result;
    }
}
