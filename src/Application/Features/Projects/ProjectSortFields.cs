namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>
/// The sort WHITELIST of the projects list: the only values SortBy may take. Infrastructure
/// maps each key to an expression; a user string never reaches OrderBy.
/// </summary>
public static class ProjectSortFields
{
    public const string Name = "name";
    public const string CreatedAt = "createdAt";
    public const string UpdatedAt = "updatedAt";

    public static readonly IReadOnlyCollection<string> All = [Name, CreatedAt, UpdatedAt];
}
