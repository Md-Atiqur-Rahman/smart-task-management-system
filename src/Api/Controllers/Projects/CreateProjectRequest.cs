namespace Datavanced.SmartTaskManagement.Api.Controllers.Projects;

/// <summary>
/// HTTP body for POST /api/projects. Fields are nullable on purpose: a missing value reaches
/// FluentValidation (400 with an errors map) instead of failing during JSON deserialization.
/// OwnerId is honoured for Admin only; it defaults to the caller.
/// </summary>
public sealed record CreateProjectRequest(string? Name, string? Description, Guid? OwnerId);
