using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListAssignableUsers;
using Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListUsers;

namespace Datavanced.SmartTaskManagement.Application.Features.Users;

/// <summary>Read side of users: returns plain values and DTOs only, never aggregates.</summary>
public interface IUserReadRepository
{
    /// <summary>The user's current token version, or null when the user does not exist.</summary>
    Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken ct);

    /// <summary>The user as shown to the signed-in client, or null when the user does not exist.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(Guid id, CancellationToken ct);

    Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct);

    /// <summary>
    /// Users the caller may assign to, sorted by full name: everyone when <paramref name="includeAllRoles"/>,
    /// otherwise all Team Members plus the caller.
    /// </summary>
    Task<PagedResult<AssignableUserDto>> ListAssignableAsync(
        ListAssignableUsersQuery query, Guid callerId, bool includeAllRoles, CancellationToken ct);
}
