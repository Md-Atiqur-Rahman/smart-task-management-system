namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>Unread notifications of the caller inside the retention window (the badge number).</summary>
public sealed record UnreadCountDto(int Count);
