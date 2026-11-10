using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks;

/// <summary>A single task with its assignee (null when unassigned) and its project.</summary>
public sealed record TaskDetailsDto(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    TaskAssigneeDto? Assignee,
    TaskProjectDto Project,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>The assignee of a task as shown in its details. Never carries credentials.</summary>
public sealed record TaskAssigneeDto(Guid Id, string FullName, string Email);

/// <summary>The project of a task as shown in its details.</summary>
public sealed record TaskProjectDto(Guid Id, string Name);
