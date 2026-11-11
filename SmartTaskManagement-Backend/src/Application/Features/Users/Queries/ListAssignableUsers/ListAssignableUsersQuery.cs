using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListAssignableUsers;

/// <summary>
/// Users the caller may assign work to, sorted by full name. Admin: all users; Project Manager:
/// all Team Members plus the caller. The caller comes from the security context.
/// </summary>
public sealed record ListAssignableUsersQuery(string? Search, int Page, int PageSize)
    : IQuery<PagedResult<AssignableUserDto>>;
