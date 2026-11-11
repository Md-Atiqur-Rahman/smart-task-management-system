using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>
/// Recipient rules of design section 3 and the dashboard-changed recipients of section 6. The actor
/// is never notified; every Admin gets the dashboard signal through <see cref="LiveEvent.IncludeAdmins"/>.
/// </summary>
internal sealed class AssignmentNotifier(
    INotificationRepository notifications,
    IUserRepository users,
    IProjectRepository projects,
    ITaskItemReadRepository taskReads,
    LiveEventBuffer liveEvents,
    TimeProvider timeProvider) : IAssignmentNotifier
{
    private string? _actorName;

    public async Task TaskAssigneeChangedAsync(
        TaskItem task, Guid? previousAssigneeId, Guid actorId, CancellationToken ct)
    {
        var ownerId = await OwnerOfAsync(task, ct);
        var newAssigneeId = task.AssigneeId;

        if (newAssigneeId != previousAssigneeId)
        {
            if (newAssigneeId is { } assigned && assigned != actorId)
            {
                await NotifyAsync(assigned, NotificationType.TaskAssigned, task.Id, null, task.Title, actorId, ct);
            }

            if (previousAssigneeId is { } unassigned && unassigned != actorId)
            {
                await NotifyAsync(unassigned, NotificationType.TaskUnassigned, null, null, task.Title, actorId, ct);
            }
        }

        DashboardChanged(actorId, previousAssigneeId, newAssigneeId, ownerId);
    }

    public async Task ProjectOwnerChangedAsync(
        Project project, Guid? previousOwnerId, Guid actorId, CancellationToken ct)
    {
        var newOwnerId = project.OwnerId;

        if (newOwnerId != previousOwnerId)
        {
            if (newOwnerId != actorId)
            {
                await NotifyAsync(
                    newOwnerId, NotificationType.ProjectOwnerAssigned, null, project.Id, project.Name, actorId, ct);
            }

            if (previousOwnerId is { } removed && removed != actorId)
            {
                await NotifyAsync(
                    removed, NotificationType.ProjectOwnerRemoved, null, null, project.Name, actorId, ct);
            }
        }

        DashboardChanged(actorId, previousOwnerId, newOwnerId);
    }

    public async Task TaskChangedAsync(TaskItem task, Guid actorId, CancellationToken ct)
    {
        var ownerId = await OwnerOfAsync(task, ct);
        DashboardChanged(actorId, task.AssigneeId, ownerId);
    }

    public async Task ProjectDeletedAsync(Project project, Guid actorId, CancellationToken ct)
    {
        var assigneeIds = await taskReads.GetAssigneeIdsAsync(project.Id, ct);
        DashboardChanged(actorId, [project.OwnerId, .. assigneeIds.Select(id => (Guid?)id)]);
    }

    private async Task<Guid?> OwnerOfAsync(TaskItem task, CancellationToken ct) =>
        (await projects.GetByIdAsync(task.ProjectId, ct))?.OwnerId;

    private async Task NotifyAsync(
        Guid recipientId,
        NotificationType type,
        Guid? taskId,
        Guid? projectId,
        string subject,
        Guid actorId,
        CancellationToken ct)
    {
        var actorName = await ActorNameAsync(actorId, ct);
        var notification = Notification.Create(
            recipientId, type, taskId, projectId, subject, actorName, timeProvider.GetUtcNow());
        notifications.Add(notification);

        var dto = new NotificationDto(
            notification.Id,
            notification.Type,
            NotificationMessages.Render(notification.Type, notification.Subject, notification.ActorName),
            notification.TaskId,
            notification.ProjectId,
            notification.CreatedAt,
            notification.IsRead);
        liveEvents.Add(new LiveEvent(LiveEventTypes.Notification, [recipientId], false, dto));
    }

    private async Task<string> ActorNameAsync(Guid actorId, CancellationToken ct)
    {
        if (_actorName is not null)
        {
            return _actorName;
        }

        // The actor passed authentication in this request; a missing user is a bug, not an expected failure.
        var actor = await users.GetByIdAsync(actorId, ct)
            ?? throw new InvalidOperationException("The acting user was not found.");
        _actorName = actor.FullName;
        return _actorName;
    }

    private void DashboardChanged(Guid actorId, params Guid?[] affected) =>
        DashboardChanged(actorId, (IEnumerable<Guid?>)affected);

    private void DashboardChanged(Guid actorId, IEnumerable<Guid?> affected)
    {
        Guid[] userIds = [.. affected.OfType<Guid>().Append(actorId).Distinct()];
        liveEvents.Add(new LiveEvent(LiveEventTypes.DashboardChanged, userIds, true, null));
    }
}
