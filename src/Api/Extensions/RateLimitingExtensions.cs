using System.Threading.RateLimiting;

using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Api.Options;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class RateLimitingExtensions
{
    public const string RateLimitedCode = "rate_limited";

    /// <summary>Built-in fixed-window limiter per client IP; rejections are ProblemDetails (429).</summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, limits) =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limits.Value.PermitLimit,
                            Window = TimeSpan.FromSeconds(limits.Value.WindowSeconds),
                            QueueLimit = 0,
                        }));
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
