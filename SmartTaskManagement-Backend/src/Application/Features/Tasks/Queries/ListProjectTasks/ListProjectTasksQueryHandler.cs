using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListProjectTasks;

/// <summary>
/// Read path: security context -> project visibility -> read repository -> PagedResult of DTOs.
/// A project the caller cannot see is reported as not found.
/// </summary>
internal sealed class ListProjectTasksQueryHandler(
    ISecurityContext securityContext, ITaskItemReadRepository readRepository)
    : IQueryHandler<ListProjectTasksQuery, PagedResult<TaskListItemDto>>
{
    public async Task<Result<PagedResult<TaskListItemDto>>> Handle(
        ListProjectTasksQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<PagedResult<TaskListItemDto>>(AuthErrors.TokenRevoked);
        }

        if (!await readRepository.IsProjectVisibleAsync(query.ProjectId, callerId, callerRole, cancellationToken))
        {
            return Result.Failure<PagedResult<TaskListItemDto>>(ProjectErrors.NotFound);
        }

        return Result.Success(
            await readRepository.ListAsync(query, query.ProjectId, callerId, callerRole, cancellationToken));
    }
}
