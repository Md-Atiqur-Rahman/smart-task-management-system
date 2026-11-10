using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>
/// Test-only startup filter: TestServer leaves Connection.RemoteIpAddress null, so this sets it
/// before the rest of the pipeline runs, simulating the address of the connecting peer.
/// </summary>
public sealed class RemoteAddressStartupFilter(IPAddress remoteAddress) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = remoteAddress;
                return nextMiddleware(context);
            });
            next(app);
        };
}
