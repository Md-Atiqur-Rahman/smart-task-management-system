using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListAssignableUsers;

/// <summary>Read path: security context -> read repository -> DTOs. Never loads an aggregate.</summary>
internal sealed class ListAssignableUsersQueryHandler(ISecurityContext securityContext, IUserReadRepository readRepository)
    : IQueryHandler<ListAssignableUsersQuery, PagedResult<AssignableUserDto>>
{
    public async Task<Result<PagedResult<AssignableUserDto>>> Handle(
        ListAssignableUsersQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId)
        {
            return Result.Failure<PagedResult<AssignableUserDto>>(AuthErrors.TokenRevoked);
        }

        var includeAllRoles = securityContext.Role == Role.Admin;
        return Result.Success(
            await readRepository.ListAssignableAsync(query, callerId, includeAllRoles, cancellationToken));
    }
}
