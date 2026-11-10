using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.CreateTask;

/// <summary>Write path: project access -> assignee rules -> aggregate factory -> write repository -> UnitOfWork.</summary>
internal sealed class CreateTaskCommandHandler(
    ISecurityContext securityContext,
    IProjectRepository projects,
    ITaskItemRepository tasks,
    IUserRepository users,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTaskCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<Guid>(AuthErrors.TokenRevoked);
        }

        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null || !ProjectAccess.CanManage(project, callerId, callerRole))
        {
            return Result.Failure<Guid>(ProjectErrors.NotFound);
        }

        if (command.AssigneeId is { } assigneeId)
        {
            var assigneeError = await TaskAssigneeRules.CheckAsync(
                assigneeId, callerId, callerRole, users, cancellationToken);
            if (assigneeError is not null)
            {
                return Result.Failure<Guid>(assigneeError);
            }
        }

        var task = TaskItem.Create(
            project.Id,
            command.Title,
            command.Description,
            command.Priority ?? TaskPriority.Medium,
            command.DueDate,
            command.AssigneeId);
        tasks.Add(task);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Success(task.Id) : Result.Failure<Guid>(saved.Error);
    }
}
