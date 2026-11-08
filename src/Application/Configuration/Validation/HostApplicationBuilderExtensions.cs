using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Contoso.TemplateApp.Application.Configuration.Validation;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Registers every FluentValidation validator in the Application assembly.</summary>
    public static TBuilder ConfigureValidation<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddValidatorsFromAssembly(
            typeof(HostApplicationBuilderExtensions).Assembly,
            ServiceLifetime.Scoped,
            includeInternalTypes: true);
        return builder;
    }
}
