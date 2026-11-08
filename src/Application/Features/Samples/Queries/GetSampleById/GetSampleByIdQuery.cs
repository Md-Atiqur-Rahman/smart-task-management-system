// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;

namespace Contoso.TemplateApp.Application.Features.Samples.Queries.GetSampleById;

public sealed record GetSampleByIdQuery(Guid Id) : IQuery<SampleDto>;
