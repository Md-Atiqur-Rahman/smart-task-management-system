using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Querying;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>
/// AsNoTracking + hand-written Select projections for the project read side. Every query
/// starts from the caller's visible set (never soft-deleted projects); the owner is read
/// from Users ignoring the users' soft-delete filter, so a project always shows its owner.
/// Task counts are SQL subqueries in the projection over the tasks the caller may see.
/// </summary>
public sealed class ProjectReadRepository(AppDbContext context) : IProjectReadRepository
{
    // The whitelist: ProjectSortFields key -> expression. Nothing else can be sorted on.
    private static readonly Dictionary<string, Expression<Func<Project, object?>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ProjectSortFields.Name] = p => p.Name,
            [ProjectSortFields.CreatedAt] = p => p.CreatedAt,
            [ProjectSortFields.UpdatedAt] = p => p.UpdatedAt,
        };

    public Task<ProjectDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role callerRole, CancellationToken ct)
    {
        var owners = Owners();
        var counted = CountedTasks(callerId, callerRole);
        return Visible(callerId, callerRole)
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProjectDetailsDto(
                p.Id,
                p.Name,
                p.Description,
                owners
                    .Where(u => u.Id == p.OwnerId)
                    .Select(u => new ProjectOwnerDto(u.Id, u.FullName, u.Email.Value))
                    .First(),
                p.CreatedAt,
                p.UpdatedAt,
                new ProjectTaskCountsDto(
                    counted.Count(t => t.ProjectId == p.Id && t.Status == TaskItemStatus.ToDo),
                    counted.Count(t => t.ProjectId == p.Id && t.Status == TaskItemStatus.InProgress),
                    counted.Count(t => t.ProjectId == p.Id && t.Status == TaskItemStatus.Completed),
                    counted.Count(t => t.ProjectId == p.Id && t.Status == TaskItemStatus.Cancelled))))
            .SingleOrDefaultAsync(ct);
    }

    public Task<PagedResult<ProjectListItemDto>> ListAsync(
        ListProjectsQuery query, Guid callerId, Role callerRole, CancellationToken ct)
    {
        var projects = Visible(callerId, callerRole);
        if (query.OwnerId is { } ownerId)
        {
            projects = projects.Where(p => p.OwnerId == ownerId);
        }

        var owners = Owners();
        var counted = CountedTasks(callerId, callerRole);
        return projects
            .ApplySearch(query.Search, p => p.Name, p => p.Description)
            .ApplySort(query.SortBy, query.SortDirection, SortMap, ProjectSortFields.CreatedAt, p => p.Id)
            .ToPagedResultAsync(
                p => new ProjectListItemDto(
                    p.Id,
                    p.Name,
                    p.OwnerId,
                    owners.Where(u => u.Id == p.OwnerId).Select(u => u.FullName).First(),
                    p.CreatedAt,
                    p.UpdatedAt,
                    counted.Count(t => t.ProjectId == p.Id),
                    counted.Count(t => t.ProjectId == p.Id && t.Status == TaskItemStatus.Completed)),
                query.Page,
                query.PageSize,
                ct);
    }

    // Admin: all; Project Manager: own projects plus projects with a task assigned to them;
    // any other role: projects with a task assigned to them. Only non-deleted tasks count.
    // Same rule as TaskItemReadRepository.IsProjectVisibleAsync; keep both identical.
    // The soft-delete checks are explicit on purpose: IgnoreQueryFilters() on the owner lookup
    // switches off the global query filters for the WHOLE query, the Projects and Tasks filters included.
    private IQueryable<Project> Visible(Guid callerId, Role callerRole)
    {
        var projects = Projects();
        var tasks = Tasks();
        return callerRole switch
        {
            Role.Admin => projects,
            Role.ProjectManager => projects.Where(p =>
                p.OwnerId == callerId || tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == callerId)),
            _ => projects.Where(p => tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == callerId)),
        };
    }

    // The tasks a caller's counts include: Admin: every task; Project Manager: every task of an
    // own project plus own assignments; any other role: own assignments. Non-deleted tasks only.
    private IQueryable<TaskItem> CountedTasks(Guid callerId, Role callerRole)
    {
        var tasks = Tasks();
        var projects = Projects();
        return callerRole switch
        {
            Role.Admin => tasks,
            Role.ProjectManager => tasks.Where(t =>
                t.AssigneeId == callerId || projects.Any(p => p.Id == t.ProjectId && p.OwnerId == callerId)),
            _ => tasks.Where(t => t.AssigneeId == callerId),
        };
    }

    private IQueryable<Project> Projects() => context.Set<Project>().Where(p => !p.IsDeleted);

    private IQueryable<TaskItem> Tasks() => context.Set<TaskItem>().Where(t => !t.IsDeleted);

    private IQueryable<User> Owners() => context.Set<User>().IgnoreQueryFilters();
}
