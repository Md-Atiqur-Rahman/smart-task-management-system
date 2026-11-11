using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListTasks;

public sealed class ListTasksQueryValidator(IOptions<PagingOptions> options)
    : TaskListQueryValidator<ListTasksQuery>(options);
