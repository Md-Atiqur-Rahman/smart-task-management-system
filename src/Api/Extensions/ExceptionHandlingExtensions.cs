using Datavanced.SmartTaskManagement.Api.Errors;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class ExceptionHandlingExtensions
{
    /// <summary>
    /// ProblemDetails for every error response, the global exception handler, and
    /// default "errorCode" / "traceId" on framework-generated problems (404, 405, 415...).
    /// </summary>
    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var extensions = context.ProblemDetails.Extensions;
            extensions.TryAdd(
                ErrorResults.ErrorCodeKey,
                "http." + (context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode));
            extensions.TryAdd(ErrorResults.TraceIdKey, ErrorResults.TraceIdFor(context.HttpContext));
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Must run first so it catches exceptions from every later middleware.</summary>
    public static WebApplication UseExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
