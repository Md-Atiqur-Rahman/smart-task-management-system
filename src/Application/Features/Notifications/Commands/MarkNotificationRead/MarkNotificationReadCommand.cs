using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkNotificationRead;

/// <summary>Marks one of the caller's notifications as read (idempotent).</summary>
public sealed record MarkNotificationReadCommand(Guid Id) : ICommand;
