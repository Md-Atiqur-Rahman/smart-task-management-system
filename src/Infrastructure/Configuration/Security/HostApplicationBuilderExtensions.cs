using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Security;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Security implementations owned by Infrastructure (password hashing).</summary>
    public static TBuilder ConfigureSecurity<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        return builder;
    }
}
