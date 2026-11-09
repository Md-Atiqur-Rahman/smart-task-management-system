using Datavanced.SmartTaskManagement.Application.Configuration.Messaging;
using Datavanced.SmartTaskManagement.Application.Configuration.Paging;
using Datavanced.SmartTaskManagement.Application.Configuration.Security;
using Datavanced.SmartTaskManagement.Application.Configuration.Validation;

using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Application.Configuration;

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
        builder.ConfigureAuthSessions();
        return builder;
    }
}
