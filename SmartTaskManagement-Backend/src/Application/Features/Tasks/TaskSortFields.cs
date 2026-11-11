namespace Datavanced.SmartTaskManagement.Application.Features.Tasks;

/// <summary>
/// The sort WHITELIST of the task lists: the only values SortBy may take. Infrastructure
/// maps each key to an expression; a user string never reaches OrderBy.
/// </summary>
public static class TaskSortFields
{
    public const string Title = "title";
    public const string Priority = "priority";
    public const string DueDate = "dueDate";
    public const string Status = "status";
    public const string CreatedAt = "createdAt";
    public const string UpdatedAt = "updatedAt";

    public static readonly IReadOnlyCollection<string> All = [Title, Priority, DueDate, Status, CreatedAt, UpdatedAt];
}
