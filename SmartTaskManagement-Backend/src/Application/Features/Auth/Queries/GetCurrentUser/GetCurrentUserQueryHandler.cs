using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Users;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>Read path: security context -> read repository -> DTO. Never loads the aggregate.</summary>
internal sealed class GetCurrentUserQueryHandler(ISecurityContext securityContext, IUserReadRepository readRepository)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } userId)
        {
            return Result.Failure<CurrentUserDto>(AuthErrors.TokenRevoked);
        }

        return Result.FromNullable(
            await readRepository.GetCurrentUserAsync(userId, cancellationToken),
            UserErrors.NotFound);
    }
}
