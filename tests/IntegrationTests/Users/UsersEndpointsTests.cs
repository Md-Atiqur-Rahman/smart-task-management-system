using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;

using static Datavanced.SmartTaskManagement.IntegrationTests.Auth.AuthApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Users;

[Collection(AuthCollectionDefinition.Name)]
public sealed class UsersEndpointsTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangeRole_OldAccessTokenOfTarget_Returns401OnNextRequest()
    {
        var admin = await CreateUserAsync(Role.Admin);
        var target = await CreateUserAsync(Role.TeamMember);

        using (var before = await GetAsync("/api/auth/me", target.Token))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var change = await PutRoleAsync(target.Id, Role.ProjectManager, admin.Token);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var after = await GetAsync("/api/auth/me", target.Token);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await after.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe("auth.token_revoked");
    }

    [Fact]
    public async Task ChangeRole_AdminDemotesSelfWithSecondAdmin_SucceedsAndOwnTokenIs401()
    {
        var admin = await CreateUserAsync(Role.Admin);
        await CreateUserAsync(Role.Admin);

        using var change = await PutRoleAsync(admin.Id, Role.TeamMember, admin.Token);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var after = await GetAsync("/api/auth/me", admin.Token);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeRole_AsProjectManager_Returns403()
    {
        var pm = await CreateUserAsync(Role.ProjectManager);
        var target = await CreateUserAsync(Role.TeamMember);

        using var response = await PutRoleAsync(target.Id, Role.Admin, pm.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRole_MissingRoleInBody_Returns400()
    {
        var admin = await CreateUserAsync(Role.Admin);
        var target = await CreateUserAsync(Role.TeamMember);
        using var request = new HttpRequestMessage(HttpMethod.Put, Uri($"/api/users/{target.Id}/role"))
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        using var response = await _api.Client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assignable_AsProjectManager_ReturnsTeamMembersAndSelfOnlyWithNameAndEmail()
    {
        var marker = Guid.NewGuid().ToString("N");
        var pm = await CreateUserAsync(Role.ProjectManager, marker);
        var otherPm = await CreateUserAsync(Role.ProjectManager, marker);
        var member = await CreateUserAsync(Role.TeamMember, marker);
        var admin = await CreateUserAsync(Role.Admin, marker);

        using var response = await GetAsync($"/api/users/assignable?search={marker}&pageSize=100", pm.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var ids = items.Select(i => i.GetProperty("id").GetGuid()).ToList();
        ids.ShouldBe([pm.Id, member.Id], ignoreOrder: true);
        ids.ShouldNotContain(admin.Id);
        ids.ShouldNotContain(otherPm.Id);
        foreach (var item in items)
        {
            item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray()
                .ShouldBe(["email", "fullName", "id"]);
        }
    }

    [Fact]
    public async Task ListUsers_SortByPassword_Returns400()
    {
        var admin = await CreateUserAsync(Role.Admin);

        using var response = await GetAsync("/api/users?sortBy=password", admin.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsRoleAndFilters()
    {
        var marker = Guid.NewGuid().ToString("N");
        var admin = await CreateUserAsync(Role.Admin, marker);
        await CreateUserAsync(Role.TeamMember, marker);

        using var response = await GetAsync($"/api/users?search={marker}&role=Admin&sortBy=email", admin.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(1);
        items[0].GetProperty("role").GetString().ShouldBe("Admin");
        items[0].TryGetProperty("passwordHash", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Assignable_AsTeamMember_Returns403()
    {
        var member = await CreateUserAsync(Role.TeamMember);

        using var response = await GetAsync("/api/users/assignable", member.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRole_TargetRefreshCookieAfterChange_IsRejectedOnRefresh()
    {
        var admin = await CreateUserAsync(Role.Admin);
        var target = await CreateUserAsync(Role.TeamMember);

        using var change = await PutRoleAsync(target.Id, Role.ProjectManager, admin.Token);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var refresh = await _api.PostAsync($"{Route}/refresh", _ct, target.Cookie, AllowedOrigin);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await refresh.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe("auth.invalid_refresh_token");
    }

    [Fact]
    public async Task ListUsers_SearchByEmailOnly_FindsUser()
    {
        var admin = await CreateUserAsync(Role.Admin);
        var target = await CreateUserAsync(Role.TeamMember);
        using var response = await GetAsync($"/api/users?search={target.Email}", admin.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([target.Id]);
    }

    public void Dispose() => _api.Dispose();

    private async Task<(Guid Id, string Token, string Email, string Cookie)> CreateUserAsync(Role role, string? marker = null)
    {
        var email = $"user-{marker ?? Guid.NewGuid().ToString("N")}-{Guid.NewGuid():N}@example.test";
        using var register = await _api.Client.PostAsJsonAsync(
            Uri($"{Route}/register"), new { email, fullName = $"User {marker}", password = Password }, _ct);
        register.EnsureSuccessStatusCode();
        var id = (await register.Content.ReadFromJsonAsync<JsonElement>(_ct))
            .GetProperty("user").GetProperty("id").GetGuid();

        if (role != Role.TeamMember)
        {
            await using var context = database.CreateDbContext();
            var user = await context.Set<User>().SingleAsync(u => u.Id == id, _ct);
            user.ChangeRole(role);
            await context.SaveChangesAsync(_ct);
        }

        using var login = await _api.Client.PostAsJsonAsync(
            Uri($"{Route}/login"), new { email, password = Password }, _ct);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("accessToken").GetString()!;
        return (id, token, email, CookieValue(login));
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Uri(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _api.Client.SendAsync(request, _ct);
    }

    private async Task<HttpResponseMessage> PutRoleAsync(Guid id, Role role, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Uri($"/api/users/{id}/role"))
        {
            Content = JsonContent.Create(new { role = role.ToString() }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _api.Client.SendAsync(request, _ct);
    }
}
