namespace Datavanced.SmartTaskManagement.Api.Controllers.Users;

/// <summary>Query string of GET /api/users/assignable. PageSize defaults to PagingOptions.DefaultPageSize.</summary>
public sealed class ListAssignableUsersRequest
{
    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int? PageSize { get; init; }
}
