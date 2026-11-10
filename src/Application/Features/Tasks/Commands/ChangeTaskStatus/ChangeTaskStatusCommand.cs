using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ChangeTaskStatus;

/// <summary>Moves a task to any status (no transition table).</summary>
public sealed record ChangeTaskStatusCommand(Guid Id, TaskItemStatus Status) : ICommand;
