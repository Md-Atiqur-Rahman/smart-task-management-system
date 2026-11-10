namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>
/// A project as shown in the projects list. The counts include only non-deleted tasks the caller
/// may see: every task for an Admin or the owning Project Manager, only the caller's own assigned
/// tasks for a caller who sees the project through assignment.
/// </summary>
public sealed record ProjectListItemDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    string OwnerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int TaskCount,
    int CompletedTaskCount);
