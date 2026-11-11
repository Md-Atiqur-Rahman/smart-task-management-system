using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Users;

/// <summary>
/// Query string of GET /api/users. SortBy keeps an empty value so the whitelist validator
/// rejects it with 400. PageSize defaults to PagingOptions.DefaultPageSize when omitted.
/// </summary>
public sealed class ListUsersRequest
{
    public string? Search { get; init; }

    public Role? Role { get; init; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    public int Page { get; init; } = 1;

    public int? PageSize { get; init; }
}
