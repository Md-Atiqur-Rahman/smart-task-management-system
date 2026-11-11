// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.ListSamples;

public sealed record ListSamplesQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize)
    : ListQuery(Search, SortBy, SortDirection, Page, PageSize), IQuery<PagedResult<SampleDto>>;
