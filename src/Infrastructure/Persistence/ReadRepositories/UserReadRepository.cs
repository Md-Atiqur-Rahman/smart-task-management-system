using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Users;
using Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListAssignableUsers;
using Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListUsers;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Querying;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>AsNoTracking + Select projections for the user read side.</summary>
public sealed class UserReadRepository(AppDbContext context) : IUserReadRepository
{
    private static readonly Expression<Func<User, UserDto>> ToDto = u =>
        new UserDto(u.Id, u.FullName, u.Email.Value, u.Role, u.CreatedAt);

    private static readonly Expression<Func<User, AssignableUserDto>> ToAssignableDto = u =>
        new AssignableUserDto(u.Id, u.FullName, u.Email.Value);

    // The whitelist: UserSortFields key -> expression. Nothing else can be sorted on.
    private static readonly Dictionary<string, Expression<Func<User, object?>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [UserSortFields.FullName] = u => u.FullName,
            [UserSortFields.Email] = u => u.Email,
            [UserSortFields.Role] = u => u.Role,
            [UserSortFields.CreatedAt] = u => u.CreatedAt,
        };

    public Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken ct) =>
        context.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.TokenVersion)
            .SingleOrDefaultAsync(ct);

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid id, CancellationToken ct)
    {
        // Email is a value object stored through a converter; project it, then read Value in memory.
        var row = await context.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.FullName, u.Email, u.Role })
            .SingleOrDefaultAsync(ct);

        return row is null ? null : new CurrentUserDto(row.Id, row.FullName, row.Email.Value, row.Role);
    }

    public Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct)
    {
        var users = SearchUsers(query.Search);
        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        return users
            .ApplySort(query.SortBy, query.SortDirection, SortMap, UserSortFields.CreatedAt, u => u.Id)
            .ToPagedResultAsync(ToDto, query.Page, query.PageSize, ct);
    }

    public Task<PagedResult<AssignableUserDto>> ListAssignableAsync(
        ListAssignableUsersQuery query, Guid callerId, bool includeAllRoles, CancellationToken ct)
    {
        var users = SearchUsers(query.Search);
        if (!includeAllRoles)
        {
            users = users.Where(u => u.Role == Role.TeamMember || u.Id == callerId);
        }

        return users
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .ToPagedResultAsync(ToAssignableDto, query.Page, query.PageSize, ct);
    }

    private IQueryable<User> SearchUsers(string? term) =>
        context.Set<User>().ApplySearch(term, u => u.FullName, u => (string)(object)u.Email);
}
