namespace Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

/// <summary>Event type names of the live stream (the SSE event name).</summary>
public static class LiveEventTypes
{
    public const string Notification = "notification";

    public const string NotificationsRead = "notifications-read";

    public const string DashboardChanged = "dashboard-changed";

    /// <summary>
    /// Keep-alive with empty data, sent when the stream opens and every heartbeat interval.
    /// <c>SseItem&lt;T&gt;</c> cannot write an SSE comment line, so the keep-alive is an event.
    /// </summary>
    public const string Heartbeat = "heartbeat";
}
