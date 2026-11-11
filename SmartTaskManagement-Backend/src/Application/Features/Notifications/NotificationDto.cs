using Datavanced.SmartTaskManagement.Domain.Notifications;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>A notification as the client sees it (list item and live event payload).</summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Message,
    Guid? TaskId,
    Guid? ProjectId,
    DateTimeOffset CreatedAt,
    bool IsRead);
