// SAMPLE: copy-source, delete or rename
using System.Net.Http.Json;

using Contoso.TemplateApp.IntegrationTests.Api;
using Contoso.TemplateApp.IntegrationTests.Infrastructure;

namespace Contoso.TemplateApp.IntegrationTests.Samples;

/// <summary>Real host + real SQL Server for the Sample endpoint tests.</summary>
public sealed class SampleApi : IDisposable
{
    public const string Route = "/api/samples";

    public SampleApi(SqlServerFixture database)
    {
        Factory = new ApiFactory().WithDatabase(database.Options);
        Client = Factory.CreateClient();
    }

    public ApiFactory Factory { get; }

    public HttpClient Client { get; }

    public static Uri Uri(string relative) => new(relative, UriKind.Relative);

    public async Task<Guid> CreateAsync(string name, CancellationToken ct, string? description = null)
    {
        using var response = await Client.PostAsJsonAsync(
            Uri(Route), new { name, description }, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
        return created.GetProperty("id").GetGuid();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}
