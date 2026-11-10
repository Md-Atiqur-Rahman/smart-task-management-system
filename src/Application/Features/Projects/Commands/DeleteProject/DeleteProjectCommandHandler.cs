using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.DeleteProject;

/// <summary>A project the caller cannot manage is reported as not found, never as forbidden.</summary>
internal sealed class DeleteProjectCommandHandler(
    ISecurityContext securityContext,
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DeleteProjectCommand>
{
    public async Task<Result> Handle(DeleteProjectCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure(AuthErrors.TokenRevoked);
        }

        var project = await projects.GetByIdAsync(command.Id, cancellationToken);
        if (project is null || !ProjectAccess.CanManage(project, callerId, callerRole))
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        project.Delete(timeProvider.GetUtcNow());
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
