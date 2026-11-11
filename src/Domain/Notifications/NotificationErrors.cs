using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Notifications;

/// <summary>Expected failures of the Notification aggregate. Codes are part of the API contract.</summary>
public static class NotificationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("notifications.not_found", "The notification was not found.");
}
