using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
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
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeProjectOwnerCommand>
{
    public async Task<Result> Handle(ChangeProjectOwnerCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is null || securityContext.Role is null)
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

        if (!project.ChangeOwner(owner.Id))
        {
            return Result.Success();
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
