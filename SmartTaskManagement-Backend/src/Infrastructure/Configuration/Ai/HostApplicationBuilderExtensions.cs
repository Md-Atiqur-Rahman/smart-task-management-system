using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Infrastructure.Ai;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Validated AiOptions (the API key is optional) and the AI text improver (a not-configured placeholder
    /// until the Gemini implementation arrives).
    /// </summary>
    public static TBuilder ConfigureAi<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

        builder.Services.AddSingleton<IAiTextImprover, UnconfiguredAiTextImprover>();
        return builder;
    }
}
