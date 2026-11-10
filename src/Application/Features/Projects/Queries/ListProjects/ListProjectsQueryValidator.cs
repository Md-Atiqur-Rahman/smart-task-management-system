using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;

public sealed class ListProjectsQueryValidator(IOptions<PagingOptions> options)
    : ListQueryValidator<ListProjectsQuery>(options, ProjectSortFields.All);
