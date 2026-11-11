using Datavanced.SmartTaskManagement.Application.Abstractions.Email;
using Datavanced.SmartTaskManagement.Infrastructure.Email;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Email;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Validated EmailOptions, the MailKit sender, the outbox processor and its worker (ADR 0017).</summary>
    public static TBuilder ConfigureEmail<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
        builder.Services.AddScoped<EmailOutboxProcessor>();
        builder.Services.AddHostedService<EmailOutboxWorker>();
        return builder;
    }
}
