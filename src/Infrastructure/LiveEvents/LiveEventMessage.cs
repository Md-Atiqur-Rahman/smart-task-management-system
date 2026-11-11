using Datavanced.SmartTaskManagement.Application.Features.Notifications;

namespace Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

/// <summary>The JSON message on the Redis channel: userIds, includeAdmins, eventType, data.</summary>
internal sealed record LiveEventMessage(
    IReadOnlyList<Guid> UserIds,
    bool IncludeAdmins,
    string EventType,
    NotificationDto? Data);
