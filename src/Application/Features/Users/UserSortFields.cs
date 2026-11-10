namespace Datavanced.SmartTaskManagement.Application.Features.Users;

/// <summary>
/// The sort WHITELIST of the users list: the only values SortBy may take. Infrastructure
/// maps each key to an expression; a user string never reaches OrderBy.
/// </summary>
public static class UserSortFields
{
    public const string FullName = "fullName";
    public const string Email = "email";
    public const string Role = "role";
    public const string CreatedAt = "createdAt";

    public static readonly IReadOnlyCollection<string> All = [FullName, Email, Role, CreatedAt];
}
