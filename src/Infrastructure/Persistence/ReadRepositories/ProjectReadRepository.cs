using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Querying;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>
/// AsNoTracking + hand-written Select projections for the project read side. Every query
/// starts from the caller's visible set (never soft-deleted projects); the owner is read
/// from Users ignoring the users' soft-delete filter, so a project always shows its owner.
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
                p.UpdatedAt))
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
                    p.UpdatedAt),
                query.Page,
                query.PageSize,
                ct);
    }

    // Admin: all; Project Manager: own projects; any other role: none (the Task module widens this).
    // The soft-delete check is explicit on purpose: IgnoreQueryFilters() on the owner lookup
    // switches off the global query filters for the WHOLE query, the Projects filter included.
    private IQueryable<Project> Visible(Guid callerId, Role callerRole)
    {
        var projects = context.Set<Project>().Where(p => !p.IsDeleted);
        return callerRole switch
        {
            Role.Admin => projects,
            Role.ProjectManager => projects.Where(p => p.OwnerId == callerId),
            _ => projects.Where(_ => false),
        };
    }

    private IQueryable<User> Owners() => context.Set<User>().IgnoreQueryFilters();
}
