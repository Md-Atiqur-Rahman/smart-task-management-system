namespace Contoso.TemplateApp.Application.Abstractions.Paging;

/// <summary>
/// Base query object for every list endpoint. SortBy is checked against a
/// whitelist by <see cref="ListQueryValidator{TQuery}"/>; it never reaches OrderBy as a string.
/// </summary>
public abstract record ListQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize);
