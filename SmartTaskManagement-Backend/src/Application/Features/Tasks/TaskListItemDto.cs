using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks;

/// <summary>A task as shown in the task lists. AssigneeName is null when the task is unassigned.</summary>
public sealed record TaskListItemDto(
    Guid Id,
    string Title,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid ProjectId,
    string ProjectName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
