using Datavanced.SmartTaskManagement.Application.Features.Notifications;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

/// <summary>
/// One event for the live stream: who gets it (<paramref name="UserIds"/>, plus every Admin when
/// <paramref name="IncludeAdmins"/>) and its optional payload.
/// </summary>
public sealed record LiveEvent(
    string Type,
    IReadOnlyList<Guid> UserIds,
    bool IncludeAdmins,
    NotificationDto? Notification);
