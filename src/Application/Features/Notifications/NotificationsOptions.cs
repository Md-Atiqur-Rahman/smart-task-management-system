using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>Notification settings, bound from the "Notifications" configuration section and validated on start.</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Notifications older than this many days are neither listed nor counted, and are cleaned up.</summary>
    [Range(1, 90)]
    public int RetentionDays { get; init; } = 7;

    /// <summary>Maximum number of notifications returned by the list.</summary>
    [Range(1, 200)]
    public int ListLimit { get; init; } = 50;

    /// <summary>Seconds between heartbeats (and token version checks) on an open live stream.</summary>
    [Range(5, 120)]
    public int HeartbeatSeconds { get; init; } = 25;

    /// <summary>Minutes between runs of the expired notification cleanup.</summary>
    [Range(1, 1440)]
    public int CleanupIntervalMinutes { get; init; } = 60;

    /// <summary>Redis pub/sub channel that carries live events between instances.</summary>
    [Required]
    public string RedisChannel { get; init; } = "notifications";
}
