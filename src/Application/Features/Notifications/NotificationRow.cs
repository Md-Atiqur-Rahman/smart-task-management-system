using Datavanced.SmartTaskManagement.Domain.Notifications;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>One notification as stored; the handler turns it into a <see cref="NotificationDto"/> with its message.</summary>
public sealed record NotificationRow(
    Guid Id,
    NotificationType Type,
    string Subject,
    string ActorName,
    Guid? TaskId,
    Guid? ProjectId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
