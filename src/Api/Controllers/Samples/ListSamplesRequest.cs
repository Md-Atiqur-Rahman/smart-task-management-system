// SAMPLE: copy-source, delete or rename
using System.ComponentModel.DataAnnotations;

using Contoso.TemplateApp.Application.Abstractions.Paging;

namespace Contoso.TemplateApp.Api.Controllers.Samples;

/// <summary>
/// Query string of GET /api/samples. SortBy keeps an empty value ("sortBy=") so
/// the whitelist validator rejects it with 400 instead of it silently becoming
/// "no sort". PageSize defaults to PagingOptions.DefaultPageSize when omitted.
/// </summary>
public sealed class ListSamplesRequest
{
    public string? Search { get; init; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    public int Page { get; init; } = 1;

    public int? PageSize { get; init; }
}
