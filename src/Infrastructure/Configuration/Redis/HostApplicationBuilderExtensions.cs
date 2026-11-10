using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Validated RedisOptions, one lazily connected multiplexer, token version store, rate limit counter, health check.</summary>
    public static TBuilder ConfigureRedis<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<RedisOptions>()
            .BindConfiguration(RedisOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();

        // Created on first use (not at startup); AbortOnConnectFail=false, so the app starts without Redis.
        builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
            ConnectionMultiplexer.Connect(
                RedisConnection.BuildConfiguration(provider.GetRequiredService<IOptions<RedisOptions>>().Value)));

        builder.Services.AddSingleton<ITokenVersionStore, RedisTokenVersionStore>();
        builder.Services.AddSingleton<IFixedWindowCounter, RedisFixedWindowCounter>();
        builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");
        return builder;
    }
}
