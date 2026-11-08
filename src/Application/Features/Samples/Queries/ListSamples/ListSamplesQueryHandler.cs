// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.ListSamples;

/// <summary>Read path: validated query object -> read repository -> PagedResult of DTOs.</summary>
public sealed class ListSamplesQueryHandler(ISampleReadRepository readRepository)
    : IQueryHandler<ListSamplesQuery, PagedResult<SampleDto>>
{
    public async Task<Result<PagedResult<SampleDto>>> Handle(
        ListSamplesQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readRepository.ListAsync(query, cancellationToken));
}
