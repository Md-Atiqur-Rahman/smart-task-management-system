namespace Datavanced.SmartTaskManagement.Api.Controllers.Projects;

/// <summary>HTTP body for PUT /api/projects/{id}. Nullable fields, see <see cref="CreateProjectRequest"/>.</summary>
public sealed record UpdateProjectRequest(string? Name, string? Description);
