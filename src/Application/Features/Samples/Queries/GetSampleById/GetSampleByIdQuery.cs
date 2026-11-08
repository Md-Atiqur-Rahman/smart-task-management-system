// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.GetSampleById;

public sealed record GetSampleByIdQuery(Guid Id) : IQuery<SampleDto>;
