using Contoso.TemplateApp.Application.Features.Samples;
using Contoso.TemplateApp.Domain.Abstractions;
using Contoso.TemplateApp.Domain.Samples;
using Contoso.TemplateApp.Infrastructure.Persistence;
using Contoso.TemplateApp.Infrastructure.Persistence.Interceptors;
using Contoso.TemplateApp.Infrastructure.Persistence.ReadRepositories;
using Contoso.TemplateApp.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Contoso.TemplateApp.Infrastructure.Configuration.Database;

public static class HostApplicationBuilderExtensions
{
    /// <summary>DbContext, UnitOfWork, write repositories, validated DatabaseOptions, dev-only migration.</summary>
    public static TBuilder ConfigureDatabase<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

        builder.Services.AddSingleton<AuditInterceptor>();
        builder.Services.AddDbContext<AppDbContext>((provider, options) => options
            .UseSqlServer(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.BuildConnectionString())
            .AddInterceptors(provider.GetRequiredService<AuditInterceptor>()));

        // Database check behind the Api's single /health endpoint.
        builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<ISampleRepository, SampleRepository>();
        builder.Services.AddScoped<ISampleReadRepository, SampleReadRepository>();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddHostedService<DevelopmentMigrationService>();
        }

        return builder;
    }
}
