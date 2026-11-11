using System.Globalization;

using Serilog;

namespace Datavanced.SmartTaskManagement.Api.Extensions;

internal static class LoggingExtensions
{
    /// <summary>
    /// Serilog (allowed to read configuration directly): levels from the "Serilog"
    /// section, sinks and enrichers registered in DI, console output.
    /// preserveStaticLogger: the host logs through its own logger instance, not the
    /// global static Log.Logger (no global state; several hosts can run in one process).
    /// </summary>
    public static IServiceCollection AddSerilogLogging(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((provider, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(provider)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("InstanceId", configuration["Instance:Id"] ?? Environment.MachineName)
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture),
            preserveStaticLogger: true);
        return services;
    }

    public static WebApplication UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        return app;
    }
}
