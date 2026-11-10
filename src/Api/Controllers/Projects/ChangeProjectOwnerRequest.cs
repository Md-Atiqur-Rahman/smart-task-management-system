namespace Datavanced.SmartTaskManagement.Api.Controllers.Projects;

/// <summary>HTTP body for PUT /api/projects/{id}/owner. Nullable so a missing value yields a 400.</summary>
public sealed record ChangeProjectOwnerRequest(Guid? OwnerId);
