// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Paging;
using Contoso.TemplateApp.Application.Features.Samples.Queries.ListSamples;

namespace Contoso.TemplateApp.Application.Features.Samples;

/// <summary>
/// Read side: returns DTOs only (no tracking, no aggregates). Declared here,
/// implemented in Infrastructure with hand-written Select projections.
/// </summary>
public interface ISampleReadRepository
{
    Task<SampleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<SampleDto>> ListAsync(ListSamplesQuery query, CancellationToken cancellationToken);
}
