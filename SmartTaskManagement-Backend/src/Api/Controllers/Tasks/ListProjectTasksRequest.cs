using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>
/// Query string of GET /api/projects/{projectId}/tasks (the project comes from the route) and the
/// base of <see cref="ListTasksRequest"/>. SortBy keeps an empty value ("sortBy=") so the
/// whitelist validator rejects it with 400. PageSize defaults to PagingOptions.DefaultPageSize.
/// </summary>
public class ListProjectTasksRequest
{
    public string? Search { get; init; }

    public TaskItemStatus? Status { get; init; }

    public TaskPriority? Priority { get; init; }

    public Guid? AssigneeId { get; init; }

    public bool Unassigned { get; init; }

    public DateOnly? DueFrom { get; init; }

    public DateOnly? DueTo { get; init; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    public int Page { get; init; } = 1;

    public int? PageSize { get; init; }
}
