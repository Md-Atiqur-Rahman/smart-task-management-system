using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>HTTP body for PUT /api/tasks/{id}/status. Nullable so a missing value yields a 400.</summary>
public sealed record ChangeTaskStatusRequest(TaskItemStatus? Status);
