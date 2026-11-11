using Datavanced.SmartTaskManagement.Application.Features.Dashboard;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>
/// Dashboard numbers in SQL (GROUP BY and COUNT) over the caller's visible sets; only the grouped
/// rows (at most four per grouping) reach memory. Every query is AsNoTracking and starts from
/// <see cref="Visibility"/>, so the numbers always match the task and project lists.
/// </summary>
public sealed class DashboardReadRepository(AppDbContext context) : IDashboardReadRepository
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(
        Guid callerId, Role callerRole, DateOnly today, CancellationToken cancellationToken)
    {
        var tasks = Visibility.VisibleTasks(context, callerId, callerRole).AsNoTracking();

        var byStatus = await tasks
            .GroupBy(t => t.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byPriority = await tasks
            .GroupBy(t => t.Priority)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var overdue = await tasks
            .CountAsync(
                t => (t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress)
                    && t.DueDate < today,
                cancellationToken);
        var totalProjects = await Visibility.VisibleProjects(context, callerId, callerRole)
            .AsNoTracking()
            .CountAsync(cancellationToken);

        int Status(TaskItemStatus key) => byStatus.Where(r => r.Key == key).Sum(r => r.Count);
        int Priority(TaskPriority key) => byPriority.Where(r => r.Key == key).Sum(r => r.Count);

        var statusCounts = new TaskStatusCountsDto(
            Status(TaskItemStatus.ToDo),
            Status(TaskItemStatus.InProgress),
            Status(TaskItemStatus.Completed),
            Status(TaskItemStatus.Cancelled));
        var priorityCounts = new TaskPriorityCountsDto(
            Priority(TaskPriority.Low),
            Priority(TaskPriority.Medium),
            Priority(TaskPriority.High),
            Priority(TaskPriority.Critical));

        return new DashboardSummaryDto(
            totalProjects,
            statusCounts.ToDo + statusCounts.InProgress + statusCounts.Completed + statusCounts.Cancelled,
            statusCounts,
            priorityCounts,
            statusCounts.Completed,
            statusCounts.ToDo + statusCounts.InProgress,
            overdue);
    }

    public async Task<IReadOnlyList<UpcomingTaskDto>> GetUpcomingAsync(
        Guid callerId, Role callerRole, DateOnly today, int upcomingDays, int upcomingLimit, CancellationToken cancellationToken)
    {
        var last = today.AddDays(upcomingDays);
        var projects = Visibility.Projects(context);
        var users = context.Set<User>().IgnoreQueryFilters();

        return await Visibility.VisibleTasks(context, callerId, callerRole)
            .AsNoTracking()
            .Where(t => (t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress)
                && t.DueDate >= today && t.DueDate <= last)
            .OrderBy(t => t.DueDate)
            .ThenByDescending(TaskRanks.Priority)
            .ThenBy(t => t.Id)
            .Take(upcomingLimit)
            .Select(t => new UpcomingTaskDto(
                t.Id,
                t.Title,
                t.DueDate ?? today,
                t.Status,
                t.Priority,
                t.ProjectId,
                projects.Where(p => p.Id == t.ProjectId).Select(p => p.Name).First(),
                t.AssigneeId,
                users.Where(u => u.Id == t.AssigneeId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
