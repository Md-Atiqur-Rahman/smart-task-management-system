using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Api.Options;
using Datavanced.SmartTaskManagement.Api.RateLimiting;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class RateLimitingExtensions
{
    public const string RateLimitedCode = "rate_limited";

    /// <summary>
    /// Framework rate limiting with the shared fixed-window limiter per client IP as the global
    /// limiter (in-memory fallback when Redis is down); rejections are ProblemDetails (429).
    /// </summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Singleton owned by the container, so its in-memory fallback limiters are disposed with it.
        services.AddSingleton<SharedFixedWindowRateLimiter>();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<SharedFixedWindowRateLimiter>((limiter, shared) =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.GlobalLimiter = shared;
                limiter.OnRejected = WriteRejectionAsync;
            });
        return services;
    }

    public static WebApplication UseRateLimiting(this WebApplication app)
    {
        app.UseRateLimiter();
        return app;
    }

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Extensions = { [ErrorResults.ErrorCodeKey] = RateLimitedCode },
            },
        });
    }
}
