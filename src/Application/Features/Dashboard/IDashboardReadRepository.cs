using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard;

/// <summary>Read side of the dashboard. Implemented in Infrastructure; scopes every number to what the caller may see.</summary>
public interface IDashboardReadRepository
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        Guid callerId, Role callerRole, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<UpcomingTaskDto>> GetUpcomingAsync(
        Guid callerId, Role callerRole, DateOnly today, int upcomingDays, int upcomingLimit, CancellationToken cancellationToken);
}
