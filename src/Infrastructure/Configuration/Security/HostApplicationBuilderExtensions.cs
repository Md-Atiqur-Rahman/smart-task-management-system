using Contoso.TemplateApp.Application.Abstractions.Security;
using Contoso.TemplateApp.Infrastructure.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Contoso.TemplateApp.Infrastructure.Configuration.Security;

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
