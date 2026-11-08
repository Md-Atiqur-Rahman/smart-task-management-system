using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Contoso.TemplateApp.Infrastructure.Configuration.Time;

public static class HostApplicationBuilderExtensions
{
    /// <summary>The system clock behind TimeProvider. Tests replace it with FakeTimeProvider.</summary>
    public static TBuilder ConfigureTime<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        return builder;
    }
}
