using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Application.Configuration.Notifications;

public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Binds and validates <see cref="NotificationsOptions"/> and registers the per-request live event
    /// buffer and assignment notifier (one per request, shared by the handler and the notifier).
    /// </summary>
    public static TBuilder ConfigureNotifications<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<NotificationsOptions>()
            .BindConfiguration(NotificationsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddScoped<LiveEventBuffer>();
        builder.Services.AddScoped<IAssignmentNotifier, AssignmentNotifier>();
        return builder;
    }
}
