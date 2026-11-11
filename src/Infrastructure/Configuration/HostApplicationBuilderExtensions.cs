using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.LiveEvents;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Seeding;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Time;

using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration;

public static class HostApplicationBuilderExtensions
{
    /// <summary>The single Infrastructure entry point, called from Program.cs.</summary>
    public static TBuilder ConfigureInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureTime();
        builder.ConfigureDatabase();
        builder.ConfigureRedis();
        builder.ConfigureLiveEvents();
        builder.ConfigureSecurity();

        // After ConfigureDatabase: hosted services start in registration order (development migration first).
        builder.ConfigureSeeding();
        return builder;
    }
}
