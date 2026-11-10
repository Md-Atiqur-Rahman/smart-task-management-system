namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>A single project with its owner and the task counts the caller may see.</summary>
public sealed record ProjectDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    ProjectOwnerDto Owner,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    ProjectTaskCountsDto TaskCounts);

/// <summary>The owner of a project as shown in its details. Never carries credentials.</summary>
public sealed record ProjectOwnerDto(Guid Id, string FullName, string Email);

/// <summary>
/// Non-deleted tasks of a project per status: every task for an Admin or the owning Project
/// Manager, only the caller's own assigned tasks for a caller who sees the project through assignment.
/// </summary>
public sealed record ProjectTaskCountsDto(int ToDo, int InProgress, int Completed, int Cancelled);
