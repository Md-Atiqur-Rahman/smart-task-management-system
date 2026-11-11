using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetDashboardSummary;

/// <summary>Read path: security context -> read repository -> DTO. Today is the UTC date from <see cref="TimeProvider"/>.</summary>
internal sealed class GetDashboardSummaryQueryHandler(
    ISecurityContext securityContext,
    IDashboardReadRepository readRepository,
    TimeProvider timeProvider)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is not { } callerRole)
        {
            return Result.Failure<DashboardSummaryDto>(AuthErrors.TokenRevoked);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return Result.Success(await readRepository.GetSummaryAsync(callerId, callerRole, today, cancellationToken));
    }
}
