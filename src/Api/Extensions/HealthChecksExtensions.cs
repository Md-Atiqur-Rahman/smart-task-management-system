namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class HealthChecksExtensions
{
    public const string HealthPath = "/health";

    /// <summary>
    /// The single health endpoint (no /alive). Checks are registered by
    /// Infrastructure (database). Anonymous on purpose: used by Docker and load
    /// balancers. Not rate limited, so probes never compete with client traffic.
    /// </summary>
    public static WebApplication UseHealthEndpoint(this WebApplication app)
    {
        app.MapHealthChecks(HealthPath).AllowAnonymous().DisableRateLimiting();
        return app;
    }
}
