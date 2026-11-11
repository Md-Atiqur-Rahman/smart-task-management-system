using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.LiveEvents;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// In-memory connection registry, Redis live event publisher and the Redis subscriber that feeds the
    /// registry. Needs the multiplexer from ConfigureRedis.
    /// </summary>
    public static TBuilder ConfigureLiveEvents<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<LiveConnectionRegistry>();
        builder.Services.AddSingleton<ILiveConnections>(provider => provider.GetRequiredService<LiveConnectionRegistry>());
        builder.Services.AddSingleton<ILiveEventPublisher, RedisLiveEventPublisher>();
        builder.Services.AddHostedService<RedisLiveEventSubscriber>();
        return builder;
    }
}
