using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.GetUnreadCount;

/// <summary>The caller's unread notification count inside the retention window.</summary>
public sealed record GetUnreadCountQuery : IQuery<UnreadCountDto>;
