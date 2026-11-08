// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Domain.Results;
using Contoso.TemplateApp.Domain.Samples;

namespace Contoso.TemplateApp.Application.Features.Samples.Queries.GetSampleById;

/// <summary>Read path: read repository -> DTO. Never loads the aggregate.</summary>
public sealed class GetSampleByIdQueryHandler(ISampleReadRepository readRepository)
    : IQueryHandler<GetSampleByIdQuery, SampleDto>
{
    public async Task<Result<SampleDto>> Handle(GetSampleByIdQuery query, CancellationToken cancellationToken) =>
        Result.FromNullable(
            await readRepository.GetByIdAsync(query.Id, cancellationToken),
            SampleErrors.NotFound);
}
