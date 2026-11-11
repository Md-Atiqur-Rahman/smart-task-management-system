using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetUpcomingTasks;

/// <summary>Read path: security context -> read repository -> DTOs; window and limit come from <see cref="DashboardOptions"/>.</summary>
internal sealed class GetUpcomingTasksQueryHandler(
    ISecurityContext securityContext,
    IDashboardReadRepository readRepository,
    IOptions<DashboardOptions> options,
    TimeProvider timeProvider)
    : IQueryHandler<GetUpcomingTasksQuery, IReadOnlyList<UpcomingTaskDto>>
{
    public async Task<Result<IReadOnlyList<UpcomingTaskDto>>> Handle(
        GetUpcomingTasksQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<IReadOnlyList<UpcomingTaskDto>>(AuthErrors.TokenRevoked);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var settings = options.Value;
        return Result.Success(await readRepository.GetUpcomingAsync(
            callerId, callerRole, today, settings.UpcomingDays, settings.UpcomingLimit, cancellationToken));
    }
}
