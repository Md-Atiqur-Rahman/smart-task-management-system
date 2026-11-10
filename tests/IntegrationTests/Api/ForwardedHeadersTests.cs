using System.Net;

using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>X-Forwarded-For is honoured only from configured proxies (empty list = direct connections).</summary>
public sealed class ForwardedHeadersTests
{
    private const string ForwardedFor = "X-Forwarded-For";

    private static readonly IPAddress PeerAddress = IPAddress.Parse("10.1.1.1");

    private static readonly Uri Uri = new("/test/anonymous", UriKind.Relative);

    [Fact]
    public async Task XForwardedFor_FromUntrustedAddress_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory()
            .WithSetting("RateLimiting:PermitLimit", "2")
            .WithSetting("RateLimiting:WindowSeconds", "60");
        using var client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter>(new RemoteAddressStartupFilter(PeerAddress)))).CreateClient();

        (await Send(client, "1.1.1.1", ct)).ShouldBe(HttpStatusCode.OK);
        (await Send(client, "2.2.2.2", ct)).ShouldBe(HttpStatusCode.OK);
        (await Send(client, "3.3.3.3", ct)).ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task XForwardedFor_FromTrustedProxy_UsesClientAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory()
            .WithSetting("RateLimiting:PermitLimit", "1")
            .WithSetting("RateLimiting:WindowSeconds", "60")
            .WithSetting("ForwardedHeaders:KnownProxies:0", PeerAddress.ToString());
        using var client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter>(new RemoteAddressStartupFilter(PeerAddress)))).CreateClient();

        (await Send(client, "1.1.1.1", ct)).ShouldBe(HttpStatusCode.OK);
        (await Send(client, "2.2.2.2", ct)).ShouldBe(HttpStatusCode.OK);
        (await Send(client, "1.1.1.1", ct)).ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public void Startup_InvalidProxyAddress_FailsValidation()
    {
        using var factory = new ApiFactory().WithSetting("ForwardedHeaders:KnownProxies:0", "not-an-ip");

        var exception = Should.Throw<Exception>(() => factory.CreateClient());

        Flatten(exception).OfType<OptionsValidationException>()
            .ShouldContain(e => e.Message.Contains("KnownProxies", StringComparison.Ordinal));
    }

    private static async Task<HttpStatusCode> Send(HttpClient client, string forwardedFor, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Uri);
        request.Headers.Add(ForwardedFor, forwardedFor);
        using var response = await client.SendAsync(request, ct);
        return response.StatusCode;
    }

    private static IEnumerable<Exception> Flatten(Exception? exception)
    {
        while (exception is not null)
        {
            yield return exception;
            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
                {
                    yield return inner;
                }
            }

            exception = exception.InnerException;
        }
    }
}
