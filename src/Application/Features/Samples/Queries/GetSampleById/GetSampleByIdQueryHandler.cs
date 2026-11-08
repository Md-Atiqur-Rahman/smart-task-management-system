// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.GetSampleById;

/// <summary>Read path: read repository -> DTO. Never loads the aggregate.</summary>
public sealed class GetSampleByIdQueryHandler(ISampleReadRepository readRepository)
    : IQueryHandler<GetSampleByIdQuery, SampleDto>
{
    public async Task<Result<SampleDto>> Handle(GetSampleByIdQuery query, CancellationToken cancellationToken) =>
        Result.FromNullable(
            await readRepository.GetByIdAsync(query.Id, cancellationToken),
            SampleErrors.NotFound);
}
