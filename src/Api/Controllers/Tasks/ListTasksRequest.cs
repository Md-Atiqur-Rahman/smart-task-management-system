namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>Query string of GET /api/tasks: the nested list's filters plus an optional project filter.</summary>
public sealed class ListTasksRequest : ListProjectTasksRequest
{
    public Guid? ProjectId { get; init; }
}
