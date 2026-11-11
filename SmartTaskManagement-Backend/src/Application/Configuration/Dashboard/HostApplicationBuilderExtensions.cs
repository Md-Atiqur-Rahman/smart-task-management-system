using Datavanced.SmartTaskManagement.Application.Features.Dashboard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Application.Configuration.Dashboard;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Binds and validates <see cref="DashboardOptions"/> (fails at start when invalid).</summary>
    public static TBuilder ConfigureDashboard<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<DashboardOptions>()
            .BindConfiguration(DashboardOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return builder;
    }
}
