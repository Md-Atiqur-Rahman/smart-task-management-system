using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>
/// HTTP body for PUT /api/tasks/{id}. A full replacement: a missing Description or DueDate clears
/// it; a missing Priority is a 400.
/// </summary>
public sealed record UpdateTaskRequest(string? Title, string? Description, TaskPriority? Priority, DateOnly? DueDate);
