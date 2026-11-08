// SAMPLE: copy-source, delete or rename
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using static Datavanced.SmartTaskManagement.IntegrationTests.Samples.SampleApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Samples;

/// <summary>GET list and GET by id through HTTP against real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class SampleReadTests(SqlServerFixture database) : IDisposable
{
    private readonly SampleApi _api = new(database);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // Unique per test, so tests sharing the database never see each other's rows.
    private readonly string _token = "t" + Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public async Task GetById_Existing_Returns200WithDto()
    {
        var id = await _api.CreateAsync("read me", _ct, description: "details");

        var body = await GetJsonAsync($"{Route}/{id}", HttpStatusCode.OK);

        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("name").GetString().ShouldBe("read me");
        body.GetProperty("description").GetString().ShouldBe("details");
        body.GetProperty("status").GetString().ShouldBe("Open");
        body.TryGetProperty("rowVersion", out _).ShouldBeFalse();
        body.TryGetProperty("isDeleted", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetById_Unknown_Returns404WithErrorCode()
    {
        var body = await GetJsonAsync($"{Route}/{Guid.NewGuid()}", HttpStatusCode.NotFound);

        body.GetProperty("errorCode").GetString().ShouldBe("sample.not_found");
    }

    [Fact]
    public async Task GetById_DeletedSample_Returns404()
    {
        var id = await _api.CreateAsync("gone", _ct);
        (await _api.Client.DeleteAsync(Uri($"{Route}/{id}"), _ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await GetJsonAsync($"{Route}/{id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonGuid_Returns404()
    {
        using var response = await _api.Client.GetAsync(Uri($"{Route}/not-a-guid"), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_PagesSearchesAndSorts()
    {
        foreach (var suffix in new[] { "c", "a", "e", "b", "d" })
        {
            await _api.CreateAsync($"{_token}-{suffix}", _ct);
        }

        var page2 = await GetJsonAsync(
            $"{Route}?search={_token}&sortBy=name&sortDirection=Descending&page=2&pageSize=2",
            HttpStatusCode.OK);

        page2.GetProperty("page").GetInt32().ShouldBe(2);
        page2.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page2.GetProperty("totalCount").GetInt32().ShouldBe(5);
        Names(page2).ShouldBe([$"{_token}-c", $"{_token}-b"]);
    }

    [Fact]
    public async Task List_DefaultPaging_UsesOptionsDefaultPageSize()
    {
        await _api.CreateAsync($"{_token}-only", _ct);

        var body = await GetJsonAsync($"{Route}?search={_token}", HttpStatusCode.OK);

        body.GetProperty("page").GetInt32().ShouldBe(1);
        body.GetProperty("pageSize").GetInt32().ShouldBe(20);
        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_ExcludesDeletedSamples()
    {
        var kept = await _api.CreateAsync($"{_token}-kept", _ct);
        var deleted = await _api.CreateAsync($"{_token}-deleted", _ct);
        (await _api.Client.DeleteAsync(Uri($"{Route}/{deleted}"), _ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var body = await GetJsonAsync($"{Route}?search={_token}", HttpStatusCode.OK);

        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
        body.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(kept);
    }

    // Review Focus 3: LIKE wildcards in user input match literally.
    [Theory]
    [InlineData("%", "pct")]
    [InlineData("_", "under")]
    [InlineData("[", "bracket")]
    public async Task Search_WithLikeWildcard_MatchesLiterally(string wildcard, string expectedSuffix)
    {
        await _api.CreateAsync($"{_token}%pct", _ct);
        await _api.CreateAsync($"{_token}_under", _ct);
        await _api.CreateAsync($"{_token}[bracket", _ct);
        await _api.CreateAsync($"{_token}xplain", _ct);

        var body = await GetJsonAsync(
            $"{Route}?search={System.Uri.EscapeDataString(_token + wildcard)}", HttpStatusCode.OK);

        Names(body).ShouldBe([$"{_token}{wildcard}{expectedSuffix}"]);
    }

    // Review Focus 2: bad list parameters are 400 with an errors map, never 500.
    [Theory]
    [InlineData("page=0", "Page")]
    [InlineData("page=2147483647&pageSize=100", "Page")]
    [InlineData("pageSize=-1", "PageSize")]
    [InlineData("pageSize=101", "PageSize")]
    [InlineData("sortDirection=sideways", "SortDirection")]
    [InlineData("sortBy=", "SortBy")]
    [InlineData("sortBy=password", "SortBy")]
    [InlineData("sortBy=name%3B%20DROP%20TABLE%20Samples", "SortBy")]
    public async Task List_BadParameter_Returns400WithErrorsMap(string query, string field)
    {
        var body = await GetJsonAsync($"{Route}?{query}", HttpStatusCode.BadRequest);

        body.GetProperty("errorCode").GetString().ShouldBe(ValidationError.ErrorCode);
        body.GetProperty("errors").EnumerateObject()
            .ShouldContain(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _api.Dispose();

    private static string[] Names(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!)];

    private async Task<JsonElement> GetJsonAsync(string path, HttpStatusCode expected)
    {
        using var response = await _api.Client.GetAsync(Uri(path), _ct);
        var text = await response.Content.ReadAsStringAsync(_ct);
        response.StatusCode.ShouldBe(expected, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
