using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListTasks;

/// <summary>Read path: security context -> read repository -> PagedResult of DTOs. Never loads an aggregate.</summary>
internal sealed class ListTasksQueryHandler(ISecurityContext securityContext, ITaskItemReadRepository readRepository)
    : IQueryHandler<ListTasksQuery, PagedResult<TaskListItemDto>>
{
    public async Task<Result<PagedResult<TaskListItemDto>>> Handle(
        ListTasksQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<PagedResult<TaskListItemDto>>(AuthErrors.TokenRevoked);
        }

        return Result.Success(
            await readRepository.ListAsync(query, query.ProjectId, callerId, callerRole, cancellationToken));
    }
}
