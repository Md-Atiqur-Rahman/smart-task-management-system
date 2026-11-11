using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Projects;

/// <summary>
/// Query string of GET /api/projects. SortBy keeps an empty value ("sortBy=") so the whitelist
/// validator rejects it with 400 instead of it silently becoming "no sort". PageSize defaults to
/// PagingOptions.DefaultPageSize when omitted.
/// </summary>
public sealed class ListProjectsRequest
{
    public string? Search { get; init; }

    public Guid? OwnerId { get; init; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    public int Page { get; init; } = 1;

    public int? PageSize { get; init; }
}
