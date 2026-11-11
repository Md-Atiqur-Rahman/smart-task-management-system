using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>
/// The real Program composes Application and Infrastructure and starts:
/// options validated on start, scoped security context resolvable.
/// </summary>
public sealed class HostCompositionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HostCompositionTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Host_Starts_WithValidatedPagingOptions()
    {
        var options = _factory.Services.GetRequiredService<IOptions<PagingOptions>>().Value;

        options.MaxPageSize.ShouldBeGreaterThanOrEqualTo(options.DefaultPageSize);
    }

    [Fact]
    public void SecurityContext_IsOneInstancePerScope_ForReaderAndSetter()
    {
        using var scope = _factory.Services.CreateScope();

        var reader = scope.ServiceProvider.GetRequiredService<ISecurityContext>();
        var setter = scope.ServiceProvider.GetRequiredService<ISecurityContextSetter>();

        reader.ShouldBeSameAs(setter);
        reader.IsAuthenticated.ShouldBeFalse();
    }
}
