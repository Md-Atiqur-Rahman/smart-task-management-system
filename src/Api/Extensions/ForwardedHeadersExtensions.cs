using System.Net;

using Datavanced.SmartTaskManagement.Api.Options;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Honours X-Forwarded-For only from the proxies listed in "ForwardedHeaders:KnownProxies".
    /// The framework defaults (loopback proxy and network) are cleared, so an empty list means
    /// direct connections and the header is never trusted from an unknown address.
    /// </summary>
    public static IServiceCollection AddTrustedForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TrustedProxyOptions>()
            .Bind(configuration.GetSection(TrustedProxyOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TrustedProxyOptions>, TrustedProxyOptionsValidator>();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<TrustedProxyOptions>>((forwarded, trusted) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                forwarded.KnownIPNetworks.Clear();
                forwarded.KnownProxies.Clear();
                foreach (var address in trusted.Value.KnownProxies)
                {
                    forwarded.KnownProxies.Add(IPAddress.Parse(address));
                }
            });
        return services;
    }

    /// <summary>
    /// The middleware is added only when proxies are configured. With both trusted lists empty
    /// the framework middleware trusts every sender, so an empty list must skip it entirely.
    /// </summary>
    public static WebApplication UseTrustedForwardedHeaders(this WebApplication app)
    {
        var trusted = app.Services.GetRequiredService<IOptions<TrustedProxyOptions>>().Value;
        if (trusted.KnownProxies.Count > 0)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
