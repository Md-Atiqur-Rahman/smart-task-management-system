using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListUsers;

/// <summary>Admin list of users; Search matches name or email, Role filters when set.</summary>
public sealed record ListUsersQuery(
    string? Search,
    Role? Role,
    string? SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize)
    : ListQuery(Search, SortBy, SortDirection, Page, PageSize), IQuery<PagedResult<UserDto>>;
