using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

/// <summary>Marks every unread notification of the caller inside the retention window as read.</summary>
public sealed record MarkAllNotificationsReadCommand : ICommand;
