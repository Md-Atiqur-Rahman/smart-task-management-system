using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.GetProjectById;

/// <summary>Read path: security context -> read repository -> DTO. Not visible is reported as not found.</summary>
internal sealed class GetProjectByIdQueryHandler(ISecurityContext securityContext, IProjectReadRepository readRepository)
    : IQueryHandler<GetProjectByIdQuery, ProjectDetailsDto>
{
    public async Task<Result<ProjectDetailsDto>> Handle(GetProjectByIdQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<ProjectDetailsDto>(AuthErrors.TokenRevoked);
        }

        return Result.FromNullable(
            await readRepository.GetByIdAsync(query.Id, callerId, callerRole, cancellationToken),
            ProjectErrors.NotFound);
    }
}
