using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Auth;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>
/// HTTP clients that carry a real access token obtained through the real auth endpoints
/// (register or login), never a hand-made token.
/// </summary>
public static class AuthenticatedClient
{
    /// <summary>Registers a new user (role TeamMember) and returns a client with its bearer token.</summary>
    public static async Task<HttpClient> ForNewTeamMemberAsync(ApiFactory factory, CancellationToken ct)
    {
        var client = factory.CreateClient();
        try
        {
            using var response = await client.PostAsJsonAsync(
                AuthApi.Uri($"{AuthApi.Route}/register"),
                new { email = AuthApi.NewEmail(), fullName = "Rahim Uddin", password = AuthApi.Password },
                ct);
            return await AttachTokenAsync(client, response, ct);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<HttpClient> AttachTokenAsync(
        HttpClient client, HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var token = body.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("The auth response has no accessToken.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
