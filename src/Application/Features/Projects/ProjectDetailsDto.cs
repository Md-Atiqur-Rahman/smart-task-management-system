namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>A single project with its owner.</summary>
public sealed record ProjectDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    ProjectOwnerDto Owner,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>The owner of a project as shown in its details. Never carries credentials.</summary>
public sealed record ProjectOwnerDto(Guid Id, string FullName, string Email);
