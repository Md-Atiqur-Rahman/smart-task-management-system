using Contoso.TemplateApp.Application.Configuration.Messaging;
using Contoso.TemplateApp.Application.Configuration.Paging;
using Contoso.TemplateApp.Application.Configuration.Security;
using Contoso.TemplateApp.Application.Configuration.Validation;

using Microsoft.Extensions.Hosting;

namespace Contoso.TemplateApp.Application.Configuration;

public static class HostApplicationBuilderExtensions
{
    /// <summary>The single Application entry point, called from Program.cs.</summary>
    public static TBuilder ConfigureApplication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureMessaging();
        builder.ConfigureValidation();
        builder.ConfigurePaging();
        builder.ConfigureSecurityContext();
        return builder;
    }
}
