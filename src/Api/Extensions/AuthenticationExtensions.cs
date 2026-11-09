using System.Text;

using Datavanced.SmartTaskManagement.Api.Middleware;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Security;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class AuthenticationExtensions
{
    /// <summary>
    /// JWT bearer validation from validated JwtOptions. Inbound claim mapping is
    /// OFF so "sub" and "role" keep their JWT names (SecurityClaimTypes).
    /// Every action declares [Authorize] or [AllowAnonymous] explicitly (enforced by
    /// an architecture test). No fallback policy: it would turn unknown routes into 401.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = SecurityClaimTypes.UserId,
                    RoleClaimType = SecurityClaimTypes.Role,
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>Authentication, then the one security-context middleware, then authorization.</summary>
    public static WebApplication UseJwtAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseMiddleware<SecurityContextMiddleware>();
        app.UseAuthorization();
        return app;
    }
}
