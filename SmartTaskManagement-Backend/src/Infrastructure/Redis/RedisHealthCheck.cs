using Microsoft.Extensions.Diagnostics.HealthChecks;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Redis;

/// <summary>Sends PING; part of the Api's single /health endpoint.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (RedisConnectionException ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
        catch (RedisTimeoutException ex)
        {
            return HealthCheckResult.Unhealthy("Redis PING timed out.", ex);
        }
    }
}
