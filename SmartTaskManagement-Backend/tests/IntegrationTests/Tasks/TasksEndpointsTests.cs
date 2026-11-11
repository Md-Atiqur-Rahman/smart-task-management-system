using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Tasks;

[Collection(AuthCollectionDefinition.Name)]
public sealed class TasksEndpointsTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private const string ProjectsPath = "/api/projects";
    private const string TasksPath = "/api/tasks";

    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crud_AsOwnerPm_RoundTrip()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm);

        using var create = await SendAsync(
            HttpMethod.Post,
            $"{ProjectsPath}/{projectId}/tasks",
            pm,
            new { title = "Write docs", description = "First", priority = "High", dueDate = "2030-05-01" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
        create.Headers.Location!.OriginalString.ShouldBe($"{TasksPath}/{id}");

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pm);
        details.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await details.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("title").GetString().ShouldBe("Write docs");
        body.GetProperty("priority").GetString().ShouldBe("High");
        body.GetProperty("status").GetString().ShouldBe("ToDo");
        body.GetProperty("dueDate").GetString().ShouldBe("2030-05-01");
        body.GetProperty("project").GetProperty("id").GetGuid().ShouldBe(projectId);

        using var update = await SendAsync(
            HttpMethod.Put, $"{TasksPath}/{id}", pm, new { title = "Write more docs", priority = "Critical" });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var afterUpdate = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pm);
        var updated = await afterUpdate.Content.ReadFromJsonAsync<JsonElement>(_ct);
        updated.GetProperty("title").GetString().ShouldBe("Write more docs");
        updated.GetProperty("priority").GetString().ShouldBe("Critical");
        updated.GetProperty("dueDate").ValueKind.ShouldBe(JsonValueKind.Null);

        (await ListIdsAsync(pm, $"{ProjectsPath}/{projectId}/tasks?priority=Critical")).ShouldContain(id);
        (await ListIdsAsync(pm, $"{TasksPath}?projectId={projectId}&pageSize=100")).ShouldContain(id);

        using var delete = await SendAsync(HttpMethod.Delete, $"{TasksPath}/{id}", pm);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var gone = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pm);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await gone.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("tasks.not_found");
        (await ListIdsAsync(pm, $"{TasksPath}?projectId={projectId}&pageSize=100")).ShouldNotContain(id);
    }

    [Fact]
    public async Task Create_WithoutPriority_IsMedium()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm);

        var id = await CreateTaskAsync(pm, projectId, new { title = "No priority" });

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pm);
        (await details.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("priority").GetString()
            .ShouldBe("Medium");
    }

    [Fact]
    public async Task Create_InAnotherPmsProject_Returns404ProjectNotFound()
    {
        var owner = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(owner);

        using var response = await SendAsync(
            HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", other, new { title = "Sneaky" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.not_found");
    }

    [Fact]
    public async Task Create_AsTeamMember_Returns403()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);

        using var response = await SendAsync(
            HttpMethod.Post, $"{ProjectsPath}/{Guid.NewGuid()}/tasks", member, new { title = "Nope" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_UnknownPriorityName_Returns400()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm);

        using var response = await SendAsync(
            HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", pm, new { title = "Bad", priority = "Urgent" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_MissingPriority_Returns400()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm);
        var id = await CreateTaskAsync(pm, projectId, new { title = "Task" });

        using var response = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}", pm, new { title = "Renamed" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListProjectTasks_InvisibleProject_Returns404()
    {
        var owner = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(owner);

        using var response = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{projectId}/tasks", other);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("projects.not_found");
    }

    [Fact]
    public async Task ListTasks_InvisibleProjectFilter_ReturnsEmptyPage()
    {
        var owner = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(owner);
        var id = await CreateTaskAsync(owner, projectId, new { title = "Hidden" });
        (await ListIdsAsync(owner, $"{TasksPath}?projectId={projectId}")).ShouldContain(id);

        using var response = await SendAsync(HttpMethod.Get, $"{TasksPath}?projectId={projectId}", other);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListTasks_AssigneeIdWithUnassigned_Returns400()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await SendAsync(
            HttpMethod.Get, $"{TasksPath}?assigneeId={Guid.NewGuid()}&unassigned=true", pm);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListTasks_UnknownStatusName_Returns400()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await SendAsync(HttpMethod.Get, $"{TasksPath}?status=Done", pm);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TaskInDeletedProject_Returns404ForAdminAndIsNotListed()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm);
        var id = await CreateTaskAsync(pm, projectId, new { title = "Doomed" });
        using var deleteProject = await SendAsync(HttpMethod.Delete, $"{ProjectsPath}/{projectId}", pm);
        deleteProject.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", admin);

        details.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await details.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("tasks.not_found");
        (await ListIdsAsync(admin, $"{TasksPath}?projectId={projectId}&pageSize=100")).ShouldNotContain(id);
    }

    [Fact]
    public async Task ListTasks_NoToken_Returns401()
    {
        using var response = await _api.Client.GetAsync(AuthApi.Uri(TasksPath), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _api.Dispose();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, TestUser user, object? body = null) =>
        _api.SendAsync(method, path, user.Token, body, _ct);

    private async Task<Guid> CreateProjectAsync(TestUser user)
    {
        using var response = await SendAsync(HttpMethod.Post, ProjectsPath, user, new { name = "Tasks project" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTaskAsync(TestUser user, Guid projectId, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", user, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    private async Task<List<Guid>> ListIdsAsync(TestUser user, string pathAndQuery)
    {
        using var response = await SendAsync(HttpMethod.Get, pathAndQuery, user);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        return [.. body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }
}
