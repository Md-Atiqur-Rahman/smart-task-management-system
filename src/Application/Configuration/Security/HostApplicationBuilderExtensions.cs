using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Application.Configuration.Security;

public static class HostApplicationBuilderExtensions
{
    /// <summary>One scoped SecurityContext per request, exposed as reader and setter.</summary>
    public static TBuilder ConfigureSecurityContext<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddScoped<SecurityContext>();
        builder.Services.AddScoped<ISecurityContext>(p => p.GetRequiredService<SecurityContext>());
        builder.Services.AddScoped<ISecurityContextSetter>(p => p.GetRequiredService<SecurityContext>());
        return builder;
    }

    /// <summary>Binds and validates <see cref="RefreshTokenOptions"/> and registers the session issuer.</summary>
    public static TBuilder ConfigureAuthSessions<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<RefreshTokenOptions>()
            .BindConfiguration(RefreshTokenOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddScoped<AuthSessionIssuer>();
        builder.Services.AddSingleton<DummyPasswordHash>();
        return builder;
    }
}
