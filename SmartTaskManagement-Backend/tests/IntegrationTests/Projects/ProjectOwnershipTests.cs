using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Projects;

[Collection(AuthCollectionDefinition.Name)]
public sealed class ProjectOwnershipTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private const string ProjectsPath = "/api/projects";

    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangeOwner_AsAdmin_PreviousOwnerGets404OnNextRequest()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var previous = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var next = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(previous, "Handover");

        using var before = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", previous);
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var change = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", admin, new { ownerId = next.Id });
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var after = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", previous);
        after.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListIdsAsync(previous)).ShouldNotContain(id);

        using var asNext = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{id}", next);
        asNext.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await asNext.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("owner").GetProperty("id").GetGuid()
            .ShouldBe(next.Id);
        (await ListIdsAsync(admin, $"?ownerId={next.Id}")).ShouldContain(id);
        (await ListIdsAsync(admin, $"?ownerId={previous.Id}")).ShouldNotContain(id);
    }

    [Fact]
    public async Task ChangeOwner_ToTeamMember_Returns400InvalidOwner()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var id = await CreateProjectAsync(pm, "Stays");

        using var response = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", admin, new { ownerId = member.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.invalid_owner");
    }

    [Fact]
    public async Task ChangeOwner_AsProjectManager_Returns403()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(pm, "Mine");

        using var response = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", pm, new { ownerId = other.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeOwner_ToCurrentOwner_Returns204()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(pm, "Same");

        using var response = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", admin, new { ownerId = pm.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListIdsAsync(admin, $"?ownerId={pm.Id}")).ShouldContain(id);
    }

    [Fact]
    public async Task ChangeOwner_DeletedProject_Returns404()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(pm, "Doomed");
        using var delete = await SendAsync(HttpMethod.Delete, $"{ProjectsPath}/{id}", pm);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", admin, new { ownerId = other.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.not_found");
    }

    [Fact]
    public async Task ChangeOwner_MissingOwnerId_Returns400()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(pm, "NoOwner");

        using var response = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", admin, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DemotePmWhoOwnsProject_Returns409UntilReassigned()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var id = await CreateProjectAsync(pm, "Anchored");
        var rolePath = $"/api/users/{pm.Id}/role";

        using var blocked = await SendAsync(HttpMethod.Put, rolePath, admin, new { role = "TeamMember" });
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("users.owns_projects");

        using var reassign = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{id}/owner", admin, new { ownerId = other.Id });
        reassign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var allowed = await SendAsync(HttpMethod.Put, rolePath, admin, new { role = "TeamMember" });
        allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
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

    private async Task<List<Guid>> ListIdsAsync(TestUser user, string query = "?pageSize=100")
    {
        if (!query.Contains("pageSize", StringComparison.Ordinal))
        {
            query += "&pageSize=100";
        }

        using var response = await SendAsync(HttpMethod.Get, ProjectsPath + query, user);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        return [.. body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }
}
