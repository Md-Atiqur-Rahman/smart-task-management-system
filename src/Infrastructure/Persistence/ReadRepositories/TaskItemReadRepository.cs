using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Querying;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>
/// AsNoTracking + hand-written Select projections for the task read side. Every query starts
/// from the caller's visible set (never a soft-deleted task, never a task of a soft-deleted
/// project); the assignee is read from Users ignoring the users' soft-delete filter, so a task
/// always shows its assignee.
/// </summary>
public sealed class TaskItemReadRepository(AppDbContext context) : ITaskItemReadRepository
{
    // The whitelist: TaskSortFields key -> expression. Nothing else can be sorted on.
    // Enums are stored as strings, so priority and status sort by an explicit rank (SQL CASE),
    // never alphabetically. A missing due date sorts as the largest date: last ascending,
    // first descending, without relying on the database's NULL ordering.
    private static readonly Dictionary<string, Expression<Func<TaskItem, object?>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [TaskSortFields.Title] = t => t.Title,
            [TaskSortFields.Priority] = t =>
                t.Priority == TaskPriority.Low ? 0
                : t.Priority == TaskPriority.Medium ? 1
                : t.Priority == TaskPriority.High ? 2
                : 3,
            [TaskSortFields.DueDate] = t => t.DueDate ?? DateOnly.MaxValue,
            [TaskSortFields.Status] = t =>
                t.Status == TaskItemStatus.ToDo ? 0
                : t.Status == TaskItemStatus.InProgress ? 1
                : t.Status == TaskItemStatus.Completed ? 2
                : 3,
            [TaskSortFields.CreatedAt] = t => t.CreatedAt,
            [TaskSortFields.UpdatedAt] = t => t.UpdatedAt,
        };

    public Task<TaskDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role callerRole, CancellationToken ct)
    {
        var projects = Projects();
        var users = Users();
        return VisibleTasks(callerId, callerRole)
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TaskDetailsDto(
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.DueDate,
                users
                    .Where(u => u.Id == t.AssigneeId)
                    .Select(u => new TaskAssigneeDto(u.Id, u.FullName, u.Email.Value))
                    .FirstOrDefault(),
                projects
                    .Where(p => p.Id == t.ProjectId)
                    .Select(p => new TaskProjectDto(p.Id, p.Name))
                    .First(),
                t.CreatedAt,
                t.UpdatedAt))
            .SingleOrDefaultAsync(ct);
    }

    public Task<PagedResult<TaskListItemDto>> ListAsync(
        TaskListQuery query, Guid? projectId, Guid callerId, Role callerRole, CancellationToken ct)
    {
        var tasks = VisibleTasks(callerId, callerRole);
        if (projectId is { } project)
        {
            tasks = tasks.Where(t => t.ProjectId == project);
        }

        if (query.Status is { } status)
        {
            tasks = tasks.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tasks = tasks.Where(t => t.Priority == priority);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            tasks = tasks.Where(t => t.AssigneeId == assigneeId);
        }

        if (query.Unassigned)
        {
            tasks = tasks.Where(t => t.AssigneeId == null);
        }

        // A task without a due date never matches a due filter (NULL comparisons are false).
        if (query.DueFrom is { } dueFrom)
        {
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate >= dueFrom);
        }

        if (query.DueTo is { } dueTo)
        {
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate <= dueTo);
        }

        var projects = Projects();
        var users = Users();
        return tasks
            .ApplySearch(query.Search, t => t.Title, t => t.Description)
            .ApplySort(query.SortBy, query.SortDirection, SortMap, TaskSortFields.CreatedAt, t => t.Id)
            .ToPagedResultAsync(
                t => new TaskListItemDto(
                    t.Id,
                    t.Title,
                    t.Status,
                    t.Priority,
                    t.DueDate,
                    t.AssigneeId,
                    users.Where(u => u.Id == t.AssigneeId).Select(u => u.FullName).FirstOrDefault(),
                    t.ProjectId,
                    projects.Where(p => p.Id == t.ProjectId).Select(p => p.Name).First(),
                    t.CreatedAt,
                    t.UpdatedAt),
                query.Page,
                query.PageSize,
                ct);
    }

    public Task<bool> IsProjectVisibleAsync(Guid projectId, Guid callerId, Role callerRole, CancellationToken ct)
    {
        var project = Projects().Where(p => p.Id == projectId);
        var tasks = Tasks();
        var visible = callerRole switch
        {
            Role.Admin => project,
            Role.ProjectManager => project.Where(p =>
                p.OwnerId == callerId || tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == callerId)),
            _ => project.Where(p => tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == callerId)),
        };

        return visible.AnyAsync(ct);
    }

    // Admin: every task; Project Manager: every task of an own project plus own assignments;
    // any other role: own assignments. Always only non-deleted tasks of non-deleted projects.
    // The soft-delete checks are explicit on purpose: IgnoreQueryFilters() on the assignee lookup
    // switches off the global query filters for the WHOLE query, the Tasks and Projects filters included.
    private IQueryable<TaskItem> VisibleTasks(Guid callerId, Role callerRole)
    {
        var tasks = Tasks();
        var projects = Projects();
        return callerRole switch
        {
            Role.Admin => tasks.Where(t => projects.Any(p => p.Id == t.ProjectId)),
            Role.ProjectManager => tasks.Where(t =>
                projects.Any(p => p.Id == t.ProjectId && (p.OwnerId == callerId || t.AssigneeId == callerId))),
            _ => tasks.Where(t => t.AssigneeId == callerId && projects.Any(p => p.Id == t.ProjectId)),
        };
    }

    private IQueryable<TaskItem> Tasks() => context.Set<TaskItem>().Where(t => !t.IsDeleted);

    private IQueryable<Project> Projects() => context.Set<Project>().Where(p => !p.IsDeleted);

    private IQueryable<User> Users() => context.Set<User>().IgnoreQueryFilters();
}
