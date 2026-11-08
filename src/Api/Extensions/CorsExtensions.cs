using Datavanced.SmartTaskManagement.Api.Options;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class CorsExtensions
{
    /// <summary>One CORS policy whose origins come from validated Options.</summary>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsPolicyOptions>()
            .Bind(configuration.GetSection(CorsPolicyOptions.SectionName))
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsPolicyOptions>>((cors, policy) =>
                cors.AddPolicy(CorsPolicyOptions.PolicyName, builder => builder
                    .WithOrigins([.. policy.Value.AllowedOrigins])
                    .AllowAnyHeader()
                    .AllowAnyMethod()));
        return services;
    }

    public static WebApplication UseCorsPolicy(this WebApplication app)
    {
        app.UseCors(CorsPolicyOptions.PolicyName);
        return app;
    }
}
