using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks;

/// <summary>
/// Read side of tasks: returns DTOs only, never aggregates. Every method sees only the tasks
/// visible to the caller: not deleted, in a project that is not deleted, and the caller is an
/// Admin, the Project Manager who owns the project, or the assignee (design section 3).
/// </summary>
public interface ITaskItemReadRepository
{
    /// <summary>The task, or null when it does not exist, is deleted or is not visible to the caller.</summary>
    Task<TaskDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role callerRole, CancellationToken ct);

    /// <summary>One page of the caller's visible tasks; the project id and the filters only narrow that set.</summary>
    Task<PagedResult<TaskListItemDto>> ListAsync(
        TaskListQuery query, Guid? projectId, Guid callerId, Role callerRole, CancellationToken ct);

    /// <summary>
    /// True when the project is not deleted and the caller is an Admin, its owning Project Manager,
    /// or the assignee of a non-deleted task in it.
    /// </summary>
    Task<bool> IsProjectVisibleAsync(Guid projectId, Guid callerId, Role callerRole, CancellationToken ct);

    /// <summary>
    /// The distinct assignee ids of the project's non-deleted tasks. Not filtered by a caller: used
    /// only to address dashboard live events (design section 6), never returned to a client.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAssigneeIdsAsync(Guid projectId, CancellationToken ct);
}
