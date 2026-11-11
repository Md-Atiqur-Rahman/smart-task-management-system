using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using FluentValidation;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListUsers;

public sealed class ListUsersQueryValidator : ListQueryValidator<ListUsersQuery>
{
    public ListUsersQueryValidator(IOptions<PagingOptions> options)
        : base(options, UserSortFields.All)
    {
        RuleFor(q => q.Role).IsInEnum();
    }
}
