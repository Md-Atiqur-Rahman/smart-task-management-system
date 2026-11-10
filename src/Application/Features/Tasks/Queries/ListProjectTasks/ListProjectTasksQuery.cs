using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListProjectTasks;

/// <summary>
/// The caller's visible tasks of one project (route). The project must be visible to the caller,
/// otherwise projects.not_found; inside it a non-manager sees only their own assigned tasks.
/// </summary>
public sealed record ListProjectTasksQuery(
    Guid ProjectId,
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
    : TaskListQuery(Search, Status, Priority, AssigneeId, Unassigned, DueFrom, DueTo, SortBy, SortDirection, Page, PageSize),
        IQuery<PagedResult<TaskListItemDto>>;
