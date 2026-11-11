using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Datavanced.SmartTaskManagement.Api.OpenApi;

/// <summary>
/// Adds the Bearer security requirement to every operation that is not [AllowAnonymous].
/// Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0
/// ("Conditionally applying security requirements", AuthOperationTransformer). The page's
/// requirement uses the Microsoft.OpenApi 1.x types (OpenApiReference); this project uses
/// Microsoft.OpenApi 2.x, so the requirement uses OpenApiSecuritySchemeReference exactly as the
/// same page's document-transformer example does. The scheme itself is declared by
/// <see cref="BearerSecuritySchemeTransformer"/>.
/// </summary>
internal sealed class AuthOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var hasAllowAnonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<AllowAnonymousAttribute>()
            .Any();

        if (hasAllowAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeName, context.Document)] = [],
        });

        return Task.CompletedTask;
    }
}
