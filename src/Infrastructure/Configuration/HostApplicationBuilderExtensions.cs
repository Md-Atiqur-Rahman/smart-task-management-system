using Contoso.TemplateApp.Infrastructure.Configuration.Database;
using Contoso.TemplateApp.Infrastructure.Configuration.Security;
using Contoso.TemplateApp.Infrastructure.Configuration.Time;

using Microsoft.Extensions.Hosting;

namespace Contoso.TemplateApp.Infrastructure.Configuration;

public static class HostApplicationBuilderExtensions
{
    /// <summary>The single Infrastructure entry point, called from Program.cs.</summary>
    public static TBuilder ConfigureInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureTime();
        builder.ConfigureDatabase();
        builder.ConfigureSecurity();
        return builder;
    }
}
