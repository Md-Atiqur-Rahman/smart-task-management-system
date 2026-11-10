using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Redis;

public static class RedisConnection
{
    /// <summary>
    /// Builds the client configuration. AbortOnConnectFail is off, so a missing Redis never
    /// stops the app from starting; operations then fail with a connection or timeout error.
    /// </summary>
    public static ConfigurationOptions BuildConfiguration(RedisOptions options)
    {
        var configuration = ConfigurationOptions.Parse(options.ConnectionString);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = options.ConnectTimeoutMs;
        configuration.SyncTimeout = options.OperationTimeoutMs;
        configuration.AsyncTimeout = options.OperationTimeoutMs;
        return configuration;
    }
}
