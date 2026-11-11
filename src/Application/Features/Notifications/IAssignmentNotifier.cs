using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>
/// Called by the task and project command handlers before they save. Adds the notifications of a
/// change (never for the actor) to the unit of work and buffers its live events; the handler
/// flushes the buffer only after a successful save (design sections 3, 5 and 6).
/// </summary>
public interface IAssignmentNotifier
{
    /// <summary>The task got its first assignee, lost it or changed it. <paramref name="previousAssigneeId"/> is null on create.</summary>
    Task TaskAssigneeChangedAsync(TaskItem task, Guid? previousAssigneeId, Guid actorId, CancellationToken ct);

    /// <summary>The project got its owner (<paramref name="previousOwnerId"/> null on create) or a new one.</summary>
    Task ProjectOwnerChangedAsync(Project project, Guid? previousOwnerId, Guid actorId, CancellationToken ct);

    /// <summary>The task was edited, changed its status or was deleted: dashboard signal only.</summary>
    Task TaskChangedAsync(TaskItem task, Guid actorId, CancellationToken ct);

    /// <summary>The project was deleted: dashboard signal only, including the assignees of its tasks.</summary>
    Task ProjectDeletedAsync(Project project, Guid actorId, CancellationToken ct);
}
