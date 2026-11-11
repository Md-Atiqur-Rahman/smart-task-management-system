using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Infrastructure.Ai;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

public static class HostApplicationBuilderExtensions
{
    /// <summary>The AI text improver (a not-configured placeholder until the Gemini implementation arrives).</summary>
    public static TBuilder ConfigureAi<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IAiTextImprover, UnconfiguredAiTextImprover>();
        return builder;
    }
}
