using System.Diagnostics;
using System.Globalization;
using System.Net;

using Datavanced.SmartTaskManagement.Api.Errors;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog.Events;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>
/// A client that disconnects is not a server error: no Error log and no 500 body
/// written to a client that is gone. Real failures, including a cancellation that
/// is not a client abort, stay 500 and are logged at Error.
/// </summary>
public sealed class ClientAbortTests
{
    private const int ClientClosedRequest = 499;

    // Real pipeline. The framework's exception-handler middleware recognises the
    // abort (Debug "The request was aborted by the client.") and answers 499.
    [Fact]
    public async Task ClientAbort_ThroughPipeline_IsNotLoggedAsError_AndIsNot500()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory()
            .WithSetting("Serilog:MinimumLevel:Default", "Debug")
            .WithSetting("Serilog:MinimumLevel:Override:Microsoft", "Debug");
        using var client = factory.CreateClient();

        await Should.ThrowAsync<Exception>(() => client.GetAsync(new Uri("/test/abort", UriKind.Relative), ct));
        var finished = await WaitForRequestFinishedAsync(factory, ct);

        finished.Properties["StatusCode"].ToString()
            .ShouldBe(ClientClosedRequest.ToString(CultureInfo.InvariantCulture));
        factory.LogSink.Events.ShouldNotContain(e => e.Level >= LogEventLevel.Error);
        factory.LogSink.Events.ShouldContain(e => e.Level == LogEventLevel.Debug
            && e.MessageTemplate.Text.Contains("aborted by the client", StringComparison.Ordinal));
    }

    // Our handler on its own: if it is ever reached for an aborted request, it must
    // not log Error or write a 500 body.
    [Fact]
    public async Task GlobalExceptionHandler_AbortedRequest_LogsInformation_WritesNothing()
    {
        using var factory = new ApiFactory();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestServices = factory.Services, RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();

        var handled = await Handler(factory).TryHandleAsync(
            context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(ClientClosedRequest);
        context.Response.Body.Length.ShouldBe(0);
        factory.LogSink.Events.ShouldNotContain(e => e.Level >= LogEventLevel.Error);
        factory.LogSink.Events.ShouldContain(e => e.Level == LogEventLevel.Information
            && e.MessageTemplate.Text.Contains("aborted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CancellationWithoutClientAbort_IsStill500_AndLoggedAsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/cancel-without-abort", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        factory.LogSink.Events.ShouldContain(e =>
            e.Level == LogEventLevel.Error && e.Exception is OperationCanceledException);
    }

    private static GlobalExceptionHandler Handler(ApiFactory factory) =>
        new(
            factory.Services.GetRequiredService<IProblemDetailsService>(),
            factory.Services.GetRequiredService<ILogger<GlobalExceptionHandler>>());

    // The client fails as soon as the server aborts; wait for the server to finish.
    private static async Task<LogEvent> WaitForRequestFinishedAsync(ApiFactory factory, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            var finished = factory.LogSink.Events.FirstOrDefault(e =>
                e.MessageTemplate.Text.StartsWith("Request finished", StringComparison.Ordinal));
            if (finished is not null)
            {
                return finished;
            }

            await Task.Delay(50, ct);
        }

        throw new TimeoutException("The aborted request did not finish within 10 seconds.");
    }
}
