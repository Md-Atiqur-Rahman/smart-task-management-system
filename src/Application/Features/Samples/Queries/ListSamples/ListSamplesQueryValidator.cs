// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Paging;

using Microsoft.Extensions.Options;

namespace Contoso.TemplateApp.Application.Features.Samples.Queries.ListSamples;

public sealed class ListSamplesQueryValidator(IOptions<PagingOptions> options)
    : ListQueryValidator<ListSamplesQuery>(options, SampleSortFields.All);
