using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Application.Configuration.Messaging;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Registers all Application handlers with the validation and logging pipeline.</summary>
    public static TBuilder ConfigureMessaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        HandlerRegistration.Register(builder.Services, typeof(HostApplicationBuilderExtensions).Assembly);
        return builder;
    }
}
