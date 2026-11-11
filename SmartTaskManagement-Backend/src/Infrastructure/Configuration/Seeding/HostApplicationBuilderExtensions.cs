using Datavanced.SmartTaskManagement.Infrastructure.Seeding;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Seeding;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Validated AdminSeedOptions, the scoped seeder and the startup hosted service. Call after
    /// ConfigureDatabase: hosted services start in registration order, so the development migration runs first.
    /// </summary>
    public static TBuilder ConfigureSeeding<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<AdminSeedOptions>()
            .BindConfiguration(AdminSeedOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AdminSeedOptions>, AdminSeedOptionsValidator>();

        builder.Services.AddScoped<AdminSeeder>();
        builder.Services.AddHostedService<AdminSeedHostedService>();
        return builder;
    }
}
