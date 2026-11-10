using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.DeleteTask;

/// <summary>Only a manager of the project may delete; anyone else (an assignee included) gets not found.</summary>
internal sealed class DeleteTaskCommandHandler(
    ISecurityContext securityContext,
    ITaskItemRepository tasks,
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DeleteTaskCommand>
{
    public async Task<Result> Handle(DeleteTaskCommand command, CancellationToken cancellationToken)
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

        task.Delete(timeProvider.GetUtcNow());
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
