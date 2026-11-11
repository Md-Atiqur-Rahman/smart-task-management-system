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
        var counted = Visibility.CountedTasks(context, callerId, callerRole);
        return Visibility.VisibleProjects(context, callerId, callerRole)
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
        var projects = Visibility.VisibleProjects(context, callerId, callerRole);
        if (query.OwnerId is { } ownerId)
        {
            projects = projects.Where(p => p.OwnerId == ownerId);
        }

        var owners = Owners();
        var counted = Visibility.CountedTasks(context, callerId, callerRole);
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

    private IQueryable<User> Owners() => context.Set<User>().IgnoreQueryFilters();
}
