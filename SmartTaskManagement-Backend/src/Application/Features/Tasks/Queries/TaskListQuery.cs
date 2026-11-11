using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

using FluentValidation;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries;

/// <summary>
/// Shared filters of the two task lists. Search matches title or description; Unassigned = true
/// keeps only tasks without an assignee; DueFrom / DueTo are inclusive and never match a task
/// without a due date. Every filter narrows the caller's visible set; none widens it.
/// </summary>
public abstract record TaskListQuery(
    string? Search,
    TaskItemStatus? Status,
    TaskPriority? Priority,
    Guid? AssigneeId,
    bool Unassigned,
    DateOnly? DueFrom,
    DateOnly? DueTo,
    string? SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize)
    : ListQuery(Search, SortBy, SortDirection, Page, PageSize);

/// <summary>Shared rules of the task lists on top of the paging and sort whitelist rules.</summary>
public abstract class TaskListQueryValidator<TQuery> : ListQueryValidator<TQuery>
    where TQuery : TaskListQuery
{
    protected TaskListQueryValidator(IOptions<PagingOptions> options)
        : base(options, TaskSortFields.All)
    {
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Priority).IsInEnum();
        RuleFor(q => q.AssigneeId)
            .Null()
            .When(q => q.Unassigned)
            .WithMessage("AssigneeId cannot be combined with Unassigned = true.");
        RuleFor(q => q.DueFrom)
            .Must((q, dueFrom) => dueFrom <= q.DueTo)
            .When(q => q.DueFrom is not null && q.DueTo is not null)
            .WithMessage("DueFrom must be on or before DueTo.");
    }
}
