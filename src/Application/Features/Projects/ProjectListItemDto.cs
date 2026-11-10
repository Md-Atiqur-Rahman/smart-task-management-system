namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>A project as shown in the projects list.</summary>
public sealed record ProjectListItemDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    string OwnerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
