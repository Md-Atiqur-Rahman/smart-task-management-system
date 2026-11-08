using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Datavanced.SmartTaskManagement.Application.Configuration.Paging;

public static class HostApplicationBuilderExtensions
{
    /// <summary>Binds and validates <see cref="PagingOptions"/> (fails at start when invalid).</summary>
    public static TBuilder ConfigurePaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOptions<PagingOptions>()
            .BindConfiguration(PagingOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => o.DefaultPageSize <= o.MaxPageSize,
                $"{nameof(PagingOptions.DefaultPageSize)} must not exceed {nameof(PagingOptions.MaxPageSize)}.")
            .ValidateOnStart();
        return builder;
    }
}
