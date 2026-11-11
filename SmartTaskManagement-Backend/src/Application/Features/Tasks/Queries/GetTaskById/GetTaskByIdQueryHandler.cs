using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.GetTaskById;

/// <summary>Read path: security context -> read repository -> DTO. Not visible is reported as not found.</summary>
internal sealed class GetTaskByIdQueryHandler(ISecurityContext securityContext, ITaskItemReadRepository readRepository)
    : IQueryHandler<GetTaskByIdQuery, TaskDetailsDto>
{
    public async Task<Result<TaskDetailsDto>> Handle(GetTaskByIdQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<TaskDetailsDto>(AuthErrors.TokenRevoked);
        }

        return Result.FromNullable(
            await readRepository.GetByIdAsync(query.Id, callerId, callerRole, cancellationToken),
            TaskErrors.NotFound);
    }
}
