// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Application.Abstractions.Paging;
using Contoso.TemplateApp.Domain.Results;

namespace Contoso.TemplateApp.Application.Features.Samples.Queries.ListSamples;

/// <summary>Read path: validated query object -> read repository -> PagedResult of DTOs.</summary>
public sealed class ListSamplesQueryHandler(ISampleReadRepository readRepository)
    : IQueryHandler<ListSamplesQuery, PagedResult<SampleDto>>
{
    public async Task<Result<PagedResult<SampleDto>>> Handle(
        ListSamplesQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readRepository.ListAsync(query, cancellationToken));
}
