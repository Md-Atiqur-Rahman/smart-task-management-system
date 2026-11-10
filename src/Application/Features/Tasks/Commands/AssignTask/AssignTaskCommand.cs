using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;

/// <summary>Assigns a task to a user; a null AssigneeId unassigns it.</summary>
public sealed record AssignTaskCommand(Guid Id, Guid? AssigneeId) : ICommand;
