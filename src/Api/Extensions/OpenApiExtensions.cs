using System.Text.Json.Serialization;

using Datavanced.SmartTaskManagement.Api.OpenApi;

using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    /// <summary>
    /// The OpenAPI document (always registered, so the build-time generator can
    /// produce docs/openapi.json) with the JWT Bearer scheme. Line endings are
    /// normalized last, so the document is identical on Windows and Linux.
    /// </summary>
    public static IServiceCollection AddOpenApiDocument(this IServiceCollection services)
    {
        // Schemas are built from the global (Microsoft.AspNetCore.Http.Json) JsonOptions, not
        // from MVC's AddJsonOptions ("Influence on OpenAPI: None"). The same string-enum
        // converter as in AddApiControllers, so enum schemas list the names the API sends.
        services.Configure<HttpJsonOptions>(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<AuthOperationTransformer>();
            options.AddDocumentTransformer<LineEndingNormalizationTransformer>();
        });
        return services;
    }

    /// <summary>
    /// /openapi/v1.json and Swagger UI (/swagger) in Development ONLY. The docs:
    /// "OpenAPI user interfaces ... should only be enabled in development environments."
    /// </summary>
    public static WebApplication UseOpenApiInDevelopment(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options =>
            options.SwaggerEndpoint($"/openapi/{DocumentName}.json", DocumentName));
        return app;
    }
}
