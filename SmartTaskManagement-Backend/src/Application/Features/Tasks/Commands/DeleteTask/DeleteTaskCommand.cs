using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.DeleteTask;

/// <summary>Soft-deletes a task.</summary>
public sealed record DeleteTaskCommand(Guid Id) : ICommand;
