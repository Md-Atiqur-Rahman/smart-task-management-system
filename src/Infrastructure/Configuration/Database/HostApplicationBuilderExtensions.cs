using Datavanced.SmartTaskManagement.Application.Features.Dashboard;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Application.Features.Samples;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Application.Features.Users;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Samples;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Interceptors;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;

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
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
        builder.Services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<ISampleReadRepository, SampleReadRepository>();
        builder.Services.AddScoped<IUserReadRepository, UserReadRepository>();
        builder.Services.AddScoped<IProjectReadRepository, ProjectReadRepository>();
        builder.Services.AddScoped<ITaskItemReadRepository, TaskItemReadRepository>();
        builder.Services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddHostedService<DevelopmentMigrationService>();
        }

        return builder;
    }
}
