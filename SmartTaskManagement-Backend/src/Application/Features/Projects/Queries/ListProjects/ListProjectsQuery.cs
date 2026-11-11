using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;

/// <summary>
/// List of the caller's visible projects; Search matches name or description, OwnerId
/// narrows the visible set when set.
/// </summary>
public sealed record ListProjectsQuery(
    string? Search,
    Guid? OwnerId,
    string? SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize)
    : ListQuery(Search, SortBy, SortDirection, Page, PageSize), IQuery<PagedResult<ProjectListItemDto>>;
