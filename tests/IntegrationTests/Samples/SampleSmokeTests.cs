// SAMPLE: copy-source, delete or rename
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using static Datavanced.SmartTaskManagement.IntegrationTests.Samples.SampleApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Samples;

/// <summary>
/// The documented Sample flow end to end (docs/sample-flow.md): every endpoint and the
/// status codes 201, 400, 200, 404, 409, 204, with ProblemDetails + errorCode on
/// every failure. Runs as part of dotnet test.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class SampleSmokeTests(SqlServerFixture database) : IAsyncLifetime, IDisposable
{
    private readonly SampleApi _api = new(database);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Smoke_AllSampleEndpoints_ReturnDocumentedStatusCodes()
    {
        var token = "smoke" + Guid.NewGuid().ToString("N")[..8];

        // POST: 201 + Location, then 400 with errors
        using var created = await _api.Client.PostAsJsonAsync(Uri(Route), new { name = token }, _ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location.ShouldNotBeNull();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();

        using var invalid = await _api.Client.PostAsJsonAsync(Uri(Route), new { name = "" }, _ct);
        (await Problem(invalid, HttpStatusCode.BadRequest)).TryGetProperty("errors", out _).ShouldBeTrue();

        // GET list: 200 PagedResult, then 400 for a bad sort
        using var list = await _api.Client.GetAsync(Uri($"{Route}?search={token}&page=1&pageSize=5"), _ct);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await list.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("totalCount").GetInt32().ShouldBe(1);

        using var badSort = await _api.Client.GetAsync(Uri($"{Route}?sortBy=nope"), _ct);
        await Problem(badSort, HttpStatusCode.BadRequest);

        // GET by id: 200, then 404
        using var found = await _api.Client.GetAsync(Uri($"{Route}/{id}"), _ct);
        found.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var missing = await _api.Client.GetAsync(Uri($"{Route}/{Guid.NewGuid()}"), _ct);
        await Problem(missing, HttpStatusCode.NotFound);

        // complete: 204, then 409 (already completed)
        using var completed = await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct);
        completed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var completedAgain = await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct);
        await Problem(completedAgain, HttpStatusCode.Conflict);

        // DELETE: 204, then 404
        using var deleted = await _api.Client.DeleteAsync(Uri($"{Route}/{id}"), _ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var deletedAgain = await _api.Client.DeleteAsync(Uri($"{Route}/{id}"), _ct);
        await Problem(deletedAgain, HttpStatusCode.NotFound);
    }

    public ValueTask InitializeAsync() => new(_api.SignInAsync(_ct));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose() => _api.Dispose();

    private async Task<JsonElement> Problem(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.ShouldBe(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("status").GetInt32().ShouldBe((int)expected);
        body.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("errorCode").GetString().ShouldNotBeNullOrWhiteSpace();
        return body;
    }
}
