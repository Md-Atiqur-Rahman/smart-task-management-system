using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Security;
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
        builder.ConfigureSecurity();
        return builder;
    }
}
