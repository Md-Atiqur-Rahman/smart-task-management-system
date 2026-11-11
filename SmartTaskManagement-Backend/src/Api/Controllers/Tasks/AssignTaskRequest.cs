namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>HTTP body for PUT /api/tasks/{id}/assignee. A null (or missing) AssigneeId unassigns the task.</summary>
public sealed record AssignTaskRequest(Guid? AssigneeId);
