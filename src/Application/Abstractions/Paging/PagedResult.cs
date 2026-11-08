namespace Contoso.TemplateApp.Application.Abstractions.Paging;

/// <summary>One page of a list. Returned directly in the response body (no wrapper).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
