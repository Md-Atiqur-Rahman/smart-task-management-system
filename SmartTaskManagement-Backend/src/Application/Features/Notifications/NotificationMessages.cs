using Datavanced.SmartTaskManagement.Domain.Notifications;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>The one place that turns a notification into its English message (list, live event, email).</summary>
public static class NotificationMessages
{
    public static string Render(NotificationType type, string subject, string actorName) => type switch
    {
        NotificationType.TaskAssigned => $"{actorName} assigned you the task \"{subject}\".",
        NotificationType.TaskUnassigned => $"{actorName} removed the task \"{subject}\" from you.",
        NotificationType.ProjectOwnerAssigned => $"{actorName} made you the owner of the project \"{subject}\".",
        NotificationType.ProjectOwnerRemoved => $"{actorName} removed your ownership of the project \"{subject}\".",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type."),
    };
}
