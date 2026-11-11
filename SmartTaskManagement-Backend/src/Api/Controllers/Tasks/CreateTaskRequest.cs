using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>
/// HTTP body for POST /api/projects/{projectId}/tasks. Fields are nullable on purpose: a missing
/// value reaches FluentValidation (400 with an errors map). A missing Priority means Medium.
/// Enum values travel as names ("High"); an unknown name is a 400 from model binding.
/// </summary>
public sealed record CreateTaskRequest(
    string? Title,
    string? Description,
    TaskPriority? Priority,
    DateOnly? DueDate,
    Guid? AssigneeId);
