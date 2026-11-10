using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListTasks;

/// <summary>
/// The caller's visible tasks across projects. ProjectId narrows the visible set: a project the
/// caller cannot see gives an empty page, not 404 (design section 5).
/// </summary>
public sealed record ListTasksQuery(
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
    int PageSize,
    Guid? ProjectId)
    : TaskListQuery(Search, Status, Priority, AssigneeId, Unassigned, DueFrom, DueTo, SortBy, SortDirection, Page, PageSize),
        IQuery<PagedResult<TaskListItemDto>>;
