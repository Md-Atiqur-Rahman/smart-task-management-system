namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>
/// HTTP body for POST /api/tasks/description/improve. The title is optional context and is never changed;
/// the description is the text to improve. Nothing is saved.
/// </summary>
public sealed record ImproveDescriptionRequest(string? Title, string? Description);
