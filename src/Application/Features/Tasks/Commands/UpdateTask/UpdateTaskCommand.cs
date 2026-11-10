using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.UpdateTask;

/// <summary>Replaces the four detail fields of a task (a null due date clears it).</summary>
public sealed record UpdateTaskCommand(
    Guid Id,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateOnly? DueDate) : ICommand;
