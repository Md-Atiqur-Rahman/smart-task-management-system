using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.CreateTask;

/// <summary>Creates a task in a project. Returns the new Id. A null priority means Medium.</summary>
public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    TaskPriority? Priority,
    DateOnly? DueDate,
    Guid? AssigneeId) : ICommand<Guid>;
