using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard;

/// <summary>A pending task due soon, with enough data for a row that links to the task.</summary>
public sealed record UpcomingTaskDto(
    Guid Id,
    string Title,
    DateOnly DueDate,
    TaskItemStatus Status,
    TaskPriority Priority,
    Guid ProjectId,
    string ProjectName,
    Guid? AssigneeId,
    string? AssigneeName);
