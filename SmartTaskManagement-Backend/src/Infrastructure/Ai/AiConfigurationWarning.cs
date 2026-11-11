using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// Logs one warning at startup when Ai:Gemini:ApiKey is not set. The host still starts: only the improve
/// endpoint is affected (it returns ai.not_configured), design 5.
/// </summary>
public sealed partial class AiConfigurationWarning(
    IOptions<AiOptions> options,
    ILogger<AiConfigurationWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Gemini.IsConfigured)
        {
            LogNotConfigured();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Ai:Gemini:ApiKey is not set; POST /api/tasks/description/improve returns ai.not_configured.")]
    private partial void LogNotConfigured();
}
