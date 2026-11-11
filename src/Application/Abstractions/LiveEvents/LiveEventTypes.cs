namespace Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

/// <summary>Event type names of the live stream (the SSE event name).</summary>
public static class LiveEventTypes
{
    public const string Notification = "notification";

    public const string NotificationsRead = "notifications-read";

    public const string DashboardChanged = "dashboard-changed";
}
