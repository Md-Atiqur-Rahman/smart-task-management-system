using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.ListNotifications;

/// <summary>The caller's notifications inside the retention window, newest first.</summary>
public sealed record ListNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>>;
