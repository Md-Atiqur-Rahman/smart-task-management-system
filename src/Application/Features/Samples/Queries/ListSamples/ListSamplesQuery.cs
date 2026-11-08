// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Application.Abstractions.Paging;

namespace Contoso.TemplateApp.Application.Features.Samples.Queries.ListSamples;

public sealed record ListSamplesQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize)
    : ListQuery(Search, SortBy, SortDirection, Page, PageSize), IQuery<PagedResult<SampleDto>>;
