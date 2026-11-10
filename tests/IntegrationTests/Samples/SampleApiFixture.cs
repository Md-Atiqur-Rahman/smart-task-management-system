// SAMPLE: copy-source, delete or rename
using System.Net.Http.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Api;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Samples;

/// <summary>
/// Real host + real SQL Server for the Sample endpoint tests. The endpoints require a token:
/// <see cref="SignInAsync"/> (call it from the test class's InitializeAsync) registers a user
/// through the real endpoint and makes <see cref="Client"/> carry its bearer token.
/// </summary>
public sealed class SampleApi : IDisposable
{
    public const string Route = "/api/samples";

    public SampleApi(SqlServerFixture database)
    {
        Factory = new ApiFactory().WithDatabase(database.Options);
        Client = Factory.CreateClient();
    }

    /// <summary>Registers a new TeamMember and replaces <see cref="Client"/> with an authenticated one.</summary>
    public async Task SignInAsync(CancellationToken ct)
    {
        var anonymous = Client;
        Client = await AuthenticatedClient.ForNewTeamMemberAsync(Factory, ct);
        anonymous.Dispose();
    }

    public ApiFactory Factory { get; }

    public HttpClient Client { get; private set; }

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
