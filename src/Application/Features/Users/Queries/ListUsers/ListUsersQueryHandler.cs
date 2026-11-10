using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListUsers;

internal sealed class ListUsersQueryHandler(IUserReadRepository readRepository)
    : IQueryHandler<ListUsersQuery, PagedResult<UserDto>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(ListUsersQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readRepository.ListAsync(query, cancellationToken));
}
