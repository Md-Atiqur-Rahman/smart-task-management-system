// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.ListSamples;

public sealed class ListSamplesQueryValidator(IOptions<PagingOptions> options)
    : ListQueryValidator<ListSamplesQuery>(options, SampleSortFields.All);
