using Datavanced.SmartTaskManagement.Infrastructure.Notifications;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Notifications;

public static class HostApplicationBuilderExtensions
{
    /// <summary>The retention cleanup and its background worker (design 9). Needs the DbContext from ConfigureDatabase.</summary>
    public static TBuilder ConfigureNotifications<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddScoped<NotificationCleanup>();
        builder.Services.AddHostedService<NotificationCleanupWorker>();
        return builder;
    }
}
