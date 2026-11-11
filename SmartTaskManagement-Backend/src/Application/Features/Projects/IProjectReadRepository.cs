using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>
/// Read side of projects: returns DTOs only, never aggregates. Every method sees only the
/// projects visible to the caller (Admin: all; Project Manager: own projects; any caller: projects
/// with a non-deleted task assigned to them). Task counts include only non-deleted tasks the caller
/// may see: all of them for an Admin or the owning Project Manager, otherwise own assignments.
/// </summary>
public interface IProjectReadRepository
{
    /// <summary>The project, or null when it does not exist, is deleted or is not visible to the caller.</summary>
    Task<ProjectDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role callerRole, CancellationToken ct);

    /// <summary>One page of the caller's visible projects; the owner filter narrows that set.</summary>
    Task<PagedResult<ProjectListItemDto>> ListAsync(
        ListProjectsQuery query, Guid callerId, Role callerRole, CancellationToken ct);
}
