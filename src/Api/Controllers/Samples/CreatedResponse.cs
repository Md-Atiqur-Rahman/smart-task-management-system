// SAMPLE: copy-source, delete or rename
namespace Contoso.TemplateApp.Api.Controllers.Samples;

/// <summary>Body of a 201 response; the Location header points to the new resource.</summary>
public sealed record CreatedResponse(Guid Id);
