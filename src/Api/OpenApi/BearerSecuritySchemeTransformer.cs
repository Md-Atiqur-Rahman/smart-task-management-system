using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Datavanced.SmartTaskManagement.Api.OpenApi;

/// <summary>
/// Adds the JWT Bearer security scheme to the OpenAPI document, which makes
/// Swagger UI show the "Authorize" button.
/// Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0
/// ("Use document transformers", BearerSecuritySchemeTransformer). Only the
/// document-level scheme is taken; the sample's loop that marks EVERY operation
/// as requiring Bearer is left out because the Sample endpoints are anonymous.
/// Phase 2: add the same page's AuthOperationTransformer for [Authorize] endpoints.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!authenticationSchemes.Any(scheme => scheme.Name == SchemeName))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "Json Web Token",
            },
        };
    }
}
