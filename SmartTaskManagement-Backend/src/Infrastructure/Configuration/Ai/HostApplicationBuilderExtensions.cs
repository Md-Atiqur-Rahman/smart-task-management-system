using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Infrastructure.Ai;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Validated AiOptions (the API key is optional), the Gemini text improver as a typed HttpClient and a
    /// startup warning when the key is missing (design 5, ADR 0020).
    /// </summary>
    public static TBuilder ConfigureAi<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

        // The per-call timeout (Ai:TimeoutSeconds) lives in GeminiTextImprover, so a timeout and a client
        // abort can be told apart; the HttpClient's own timeout is off.
        builder.Services.AddHttpClient<IAiTextImprover, GeminiTextImprover>((provider, client) =>
        {
            client.BaseAddress = provider.GetRequiredService<IOptions<AiOptions>>().Value.Gemini.BaseUrl;
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        builder.Services.AddHostedService<AiConfigurationWarning>();
        return builder;
    }
}
