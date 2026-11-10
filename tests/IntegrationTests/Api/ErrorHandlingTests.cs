using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using Serilog.Events;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>One ErrorType -> status mapping, ProblemDetails with errorCode, generic 500 for unhandled exceptions.</summary>
public sealed class ErrorHandlingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ErrorType.Validation, HttpStatusCode.BadRequest)]
    [InlineData(ErrorType.NotFound, HttpStatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ErrorType.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(ErrorType.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(ErrorType.Unavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task ErrorType_MapsToStatus_WithProblemDetailsAndErrorCode(
        ErrorType type, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/test/errors/{type}", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("status").GetInt32().ShouldBe((int)expected);
        body.GetProperty("errorCode").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidationError_HasErrorsMap()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/errors/Validation", UriKind.Relative), _ct);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe(ValidationError.ErrorCode);
        body.GetProperty("errors").GetProperty("Name")[0].GetString().ShouldBe("Name is required.");
    }

    // Review Focus: 500 is generic to the client, details reach the log.
    [Fact]
    public async Task UnhandledException_Returns500Generic_AndLogsDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/throw", UriKind.Relative), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync(_ct);
        text.ShouldNotContain(TestEndpointsController.SecretDetail);
        var body = JsonDocument.Parse(text).RootElement;
        body.GetProperty("errorCode").GetString().ShouldBe("internal_error");

        factory.LogSink.Events.ShouldContain(e =>
            e.Level == LogEventLevel.Error
            && e.Exception != null
            && e.Exception.Message == TestEndpointsController.SecretDetail,
            "Captured: " + string.Join(" | ", factory.LogSink.Events.Select(
                e => $"{e.Level}:{e.MessageTemplate.Text}:{e.Exception?.Message}")));
    }
}
