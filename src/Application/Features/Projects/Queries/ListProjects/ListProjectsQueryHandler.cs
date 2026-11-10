using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;

/// <summary>Read path: security context -> read repository -> PagedResult of DTOs. Never loads an aggregate.</summary>
internal sealed class ListProjectsQueryHandler(ISecurityContext securityContext, IProjectReadRepository readRepository)
    : IQueryHandler<ListProjectsQuery, PagedResult<ProjectListItemDto>>
{
    public async Task<Result<PagedResult<ProjectListItemDto>>> Handle(
        ListProjectsQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<PagedResult<ProjectListItemDto>>(AuthErrors.TokenRevoked);
        }

        return Result.Success(await readRepository.ListAsync(query, callerId, callerRole, cancellationToken));
    }
}
