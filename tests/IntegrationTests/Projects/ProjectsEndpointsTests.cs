using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Projects;

[Collection(AuthCollectionDefinition.Name)]
public sealed class ProjectsEndpointsTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private const string ProjectsPath = "/api/projects";

    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crud_AsProjectManager_RoundTrip()
    {
        var marker = Guid.NewGuid().ToString("N");
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct, marker);

        using var create = await SendAsync(HttpMethod.Post, ProjectsPath, pm, new { name = "Alpha", description = "First" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
        create.Headers.Location!.OriginalString.ShouldBe($"{ProjectsPath}/{id}");

        using var details = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", pm);
        details.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await details.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("name").GetString().ShouldBe("Alpha");
        var owner = body.GetProperty("owner");
        owner.GetProperty("id").GetGuid().ShouldBe(pm.Id);
        owner.GetProperty("fullName").GetString().ShouldBe($"User {marker}");
        owner.GetProperty("email").GetString().ShouldBe(pm.Email);

        using var update = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}", pm, new { name = "Beta", description = "Second" });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var afterUpdate = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", pm);
        (await afterUpdate.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("name").GetString().ShouldBe("Beta");

        (await ListIdsAsync(pm, "?pageSize=100")).ShouldContain(id);

        using var delete = await SendAsync(HttpMethod.Delete, $"{ProjectsPath}/{id}", pm);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var gone = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", pm);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await gone.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.not_found");

        (await ListIdsAsync(pm, "?pageSize=100")).ShouldNotContain(id);
    }

    [Fact]
    public async Task Create_WhitespaceName_Returns400WithErrorsName()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await SendAsync(HttpMethod.Post, ProjectsPath, pm, new { name = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errors").TryGetProperty("Name", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_AsAdminWithUnknownOwner_Returns400InvalidOwner()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);

        using var response = await SendAsync(
            HttpMethod.Post, ProjectsPath, admin, new { name = "Alpha", ownerId = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.invalid_owner");
    }

    [Fact]
    public async Task Create_AsProjectManagerForAnotherPm_Returns400OwnerNotAllowed()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var otherPm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await SendAsync(
            HttpMethod.Post, ProjectsPath, pm, new { name = "Alpha", ownerId = otherPm.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.owner_not_allowed");
    }

    [Fact]
    public async Task Create_AsTeamMember_Returns403()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);

        using var response = await SendAsync(HttpMethod.Post, ProjectsPath, member, new { name = "Alpha" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsTeamMember_Returns200EmptyPage()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);

        using var response = await SendAsync(HttpMethod.Get, ProjectsPath, member);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task OtherPmsProject_Returns404OnDetailsUpdateDeleteAndIsNotListed()
    {
        var owner = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(owner, "Private");

        using var details = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", other);
        using var update = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}", other, new { name = "Hijack" });
        using var delete = await SendAsync(HttpMethod.Delete, $"{ProjectsPath}/{id}", other);

        details.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListIdsAsync(other, "?pageSize=100")).ShouldNotContain(id);
    }

    [Fact]
    public async Task DeletedProject_Returns404ForAdminAndIsNotListed()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(pm, "Doomed");
        using var delete = await SendAsync(HttpMethod.Delete, $"{ProjectsPath}/{id}", pm);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var details = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", admin);

        details.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListIdsAsync(admin, "?pageSize=100")).ShouldNotContain(id);
    }

    [Fact]
    public async Task List_SortByUnknownKey_Returns400()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await SendAsync(HttpMethod.Get, $"{ProjectsPath}?sortBy=password", pm);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_NoToken_Returns401()
    {
        using var response = await _api.Client.GetAsync(AuthApi.Uri(ProjectsPath), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _api.Dispose();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, TestUser user, object? body = null) =>
        _api.SendAsync(method, path, user.Token, body, _ct);

    private async Task<Guid> CreateProjectAsync(TestUser user, string name)
    {
        using var response = await SendAsync(HttpMethod.Post, ProjectsPath, user, new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    private async Task<List<Guid>> ListIdsAsync(TestUser user, string query)
    {
        using var response = await SendAsync(HttpMethod.Get, ProjectsPath + query, user);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        return [.. body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }
}
