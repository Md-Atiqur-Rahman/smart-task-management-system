using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using FluentValidation;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListAssignableUsers;

public sealed class ListAssignableUsersQueryValidator : AbstractValidator<ListAssignableUsersQuery>
{
    public ListAssignableUsersQueryValidator(IOptions<PagingOptions> options)
    {
        var maxPageSize = options.Value.MaxPageSize;

        RuleFor(q => q.Page).InclusiveBetween(1, int.MaxValue / maxPageSize);
        RuleFor(q => q.PageSize).InclusiveBetween(1, maxPageSize);
        RuleFor(q => q.Search).MaximumLength(ListQueryValidator<ListUsers.ListUsersQuery>.MaxSearchLength);
    }
}
