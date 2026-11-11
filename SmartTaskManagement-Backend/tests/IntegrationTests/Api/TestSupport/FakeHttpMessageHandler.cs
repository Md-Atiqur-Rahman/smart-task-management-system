using System.Net;
using System.Text;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>
/// Stands in for a remote HTTP API: records every request (with its body, read before the request is
/// disposed) and answers with the scripted function. No network call is ever made.
/// </summary>
public sealed class FakeHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    public static FakeHttpMessageHandler Json(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new RecordedRequest(request.Method, request.RequestUri, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)), body));
        return await respond(request, cancellationToken);
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, IReadOnlyDictionary<string, string> Headers, string? Body);
