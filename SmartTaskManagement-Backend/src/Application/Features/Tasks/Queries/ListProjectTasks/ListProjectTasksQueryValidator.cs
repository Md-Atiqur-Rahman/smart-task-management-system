using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using FluentValidation;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListProjectTasks;

public sealed class ListProjectTasksQueryValidator : TaskListQueryValidator<ListProjectTasksQuery>
{
    public ListProjectTasksQueryValidator(IOptions<PagingOptions> options)
        : base(options)
    {
        RuleFor(q => q.ProjectId).NotEmpty();
    }
}
