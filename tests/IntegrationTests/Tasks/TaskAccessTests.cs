using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Tasks;

[Collection(AuthCollectionDefinition.Name)]
public sealed class TaskAccessTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private const string ProjectsPath = "/api/projects";
    private const string TasksPath = "/api/tasks";

    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task TeamMember_SeesOnlyAssignedTasks_AndCanChangeTheirStatus()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pm, "Access A");
        var mine = await CreateTaskAsync(pm, projectId, new { title = "Mine", assigneeId = member.Id });
        var others = await CreateTaskAsync(pm, projectId, new { title = "Others" });

        var visible = await ListIdsAsync(member, $"{TasksPath}?projectId={projectId}&pageSize=100");
        visible.ShouldContain(mine);
        visible.ShouldNotContain(others);
        (await ListIdsAsync(member, $"{ProjectsPath}/{projectId}/tasks?pageSize=100")).ShouldBe([mine]);

        using var change = await SendAsync(HttpMethod.Put, $"{TasksPath}/{mine}/status", member, new { status = "InProgress" });
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{mine}", member);
        (await details.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("status").GetString()
            .ShouldBe("InProgress");
    }

    [Fact]
    public async Task TeamMember_Gets403OnEditAssignDelete_And404OnOthersTask()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pm, "Access B");
        var mine = await CreateTaskAsync(pm, projectId, new { title = "Mine", assigneeId = member.Id });
        var others = await CreateTaskAsync(pm, projectId, new { title = "Others" });

        using var edit = await SendAsync(
            HttpMethod.Put, $"{TasksPath}/{mine}", member, new { title = "Hack", priority = "Low" });
        edit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var assign = await SendAsync(
            HttpMethod.Put, $"{TasksPath}/{mine}/assignee", member, new { assigneeId = member.Id });
        assign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var delete = await SendAsync(HttpMethod.Delete, $"{TasksPath}/{mine}", member);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var get = await SendAsync(HttpMethod.Get, $"{TasksPath}/{others}", member);
        await ShouldBeNotFoundAsync(get);
        using var status = await SendAsync(
            HttpMethod.Put, $"{TasksPath}/{others}/status", member, new { status = "Completed" });
        await ShouldBeNotFoundAsync(status);
    }

    [Fact]
    public async Task Status_CompletedBackToToDo_Returns204()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm, "Access C");
        var id = await CreateTaskAsync(pm, projectId, new { title = "Reopen" });

        using var complete = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", pm, new { status = "Completed" });
        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var reopen = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", pm, new { status = "ToDo" });
        reopen.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pm);
        (await details.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("status").GetString()
            .ShouldBe("ToDo");
    }

    [Fact]
    public async Task Status_UnknownName_Returns400()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm, "Access D");
        var id = await CreateTaskAsync(pm, projectId, new { title = "Task" });

        using var unknown = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", pm, new { status = "Done" });
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var missing = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", pm, new { });
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unassign_PreviousAssigneeGets404AndLosesProjectWhenOnlyTask()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var marker = Guid.NewGuid().ToString("N");
        var projectId = await CreateProjectAsync(pm, $"Unassign {marker}");
        var id = await CreateTaskAsync(pm, projectId, new { title = "Only", assigneeId = member.Id });

        using var before = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{projectId}", member);
        before.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListIdsAsync(member, $"{ProjectsPath}?search={marker}")).ShouldContain(projectId);

        using var unassign = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", pm, new { assigneeId = (Guid?)null });
        unassign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var task = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", member);
        await ShouldBeNotFoundAsync(task);
        using var project = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{projectId}", member);
        project.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListIdsAsync(member, $"{ProjectsPath}?search={marker}")).ShouldNotContain(projectId);

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pm);
        (await details.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("assignee").ValueKind
            .ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task AdminAssignsOtherPm_ThatPmSeesTaskAndProject_ChangesStatus_Gets404OnEdit()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var owner = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(owner, "Access F");
        var id = await CreateTaskAsync(owner, projectId, new { title = "Delegated" });

        using var assign = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", admin, new { assigneeId = other.Id });
        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var task = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", other);
        task.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var project = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{projectId}", other);
        project.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var status = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", other, new { status = "InProgress" });
        status.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var edit = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}", other, new { title = "Mine now", priority = "Low" });
        await ShouldBeNotFoundAsync(edit);
    }

    [Fact]
    public async Task PmAssignsAnotherPm_Returns400AssigneeNotAllowed()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var other = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm, "Access G");
        var id = await CreateTaskAsync(pm, projectId, new { title = "Task" });

        using var response = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", pm, new { assigneeId = other.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("tasks.assignee_not_allowed");
    }

    [Fact]
    public async Task AssignUnknownUser_Returns400InvalidAssignee()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pm, "Access H");
        var id = await CreateTaskAsync(pm, projectId, new { title = "Task" });

        using var response = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", pm, new { assigneeId = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("tasks.invalid_assignee");
    }

    [Fact]
    public async Task PreviousOwnerAfterOwnerChange_KeepsAssignedTask_Gets404OnEdit()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);
        var pmA = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var pmB = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(pmA, "Access I");
        var id = await CreateTaskAsync(pmA, projectId, new { title = "Self assigned" });
        using var assign = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", pmA, new { assigneeId = pmA.Id });
        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var change = await SendAsync(HttpMethod.Put, $"{ProjectsPath}/{projectId}/owner", admin, new { ownerId = pmB.Id });
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var get = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pmA);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var status = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", pmA, new { status = "InProgress" });
        status.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var edit = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}", pmA, new { title = "Nope", priority = "Low" });
        await ShouldBeNotFoundAsync(edit);
    }

    [Fact]
    public async Task ProjectList_ShowsTaskCountsForOwnerAndOwnCountsForAssignee()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var marker = Guid.NewGuid().ToString("N");
        var projectId = await CreateProjectAsync(pm, $"Counts {marker}");
        await CreateTaskAsync(pm, projectId, new { title = "Assigned", assigneeId = member.Id });
        var done = await CreateTaskAsync(pm, projectId, new { title = "Done" });
        using var complete = await SendAsync(HttpMethod.Put, $"{TasksPath}/{done}/status", pm, new { status = "Completed" });
        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var owner = await FindProjectAsync(pm, marker, projectId);
        owner.GetProperty("taskCount").GetInt32().ShouldBe(2);
        owner.GetProperty("completedTaskCount").GetInt32().ShouldBe(1);
        var assignee = await FindProjectAsync(member, marker, projectId);
        assignee.GetProperty("taskCount").GetInt32().ShouldBe(1);
        assignee.GetProperty("completedTaskCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task OtherPm_Gets404OnEveryTaskEndpoint_AndTaskIsUnchanged()
    {
        var pmA = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var pmB = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pmA, "Cross PM");
        var id = await CreateTaskAsync(pmA, projectId, new { title = "Original", priority = "High", assigneeId = member.Id });

        using var get = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pmB);
        await ShouldBeNotFoundAsync(get);
        using var edit = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}", pmB, new { title = "Hijacked", priority = "Low" });
        await ShouldBeNotFoundAsync(edit);
        using var status = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/status", pmB, new { status = "Completed" });
        await ShouldBeNotFoundAsync(status);
        using var assign = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", pmB, new { assigneeId = (Guid?)null });
        await ShouldBeNotFoundAsync(assign);
        using var delete = await SendAsync(HttpMethod.Delete, $"{TasksPath}/{id}", pmB);
        await ShouldBeNotFoundAsync(delete);

        using var details = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", pmA);
        details.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await details.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("title").GetString().ShouldBe("Original");
        body.GetProperty("priority").GetString().ShouldBe("High");
        body.GetProperty("status").GetString().ShouldBe("ToDo");
        body.GetProperty("assignee").GetProperty("id").GetGuid().ShouldBe(member.Id);
    }

    [Fact]
    public async Task Reassign_PreviousAssigneeLosesTaskAndProject_NewAssigneeSeesTask()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var memberA = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var memberB = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var marker = Guid.NewGuid().ToString("N");
        var projectId = await CreateProjectAsync(pm, $"Reassign {marker}");
        var id = await CreateTaskAsync(pm, projectId, new { title = "Moving", assigneeId = memberA.Id });
        (await ListIdsAsync(memberA, $"{ProjectsPath}?search={marker}")).ShouldContain(projectId);

        using var reassign = await SendAsync(HttpMethod.Put, $"{TasksPath}/{id}/assignee", pm, new { assigneeId = memberB.Id });
        reassign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var oldTask = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", memberA);
        await ShouldBeNotFoundAsync(oldTask);
        using var oldProject = await SendAsync(HttpMethod.Get, $"{ProjectsPath}/{projectId}", memberA);
        oldProject.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListIdsAsync(memberA, $"{ProjectsPath}?search={marker}")).ShouldNotContain(projectId);

        using var newTask = await SendAsync(HttpMethod.Get, $"{TasksPath}/{id}", memberB);
        newTask.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await newTask.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("assignee").GetProperty("id").GetGuid()
            .ShouldBe(memberB.Id);
    }

    public void Dispose() => _api.Dispose();

    private async Task ShouldBeNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("tasks.not_found");
    }

    private async Task<JsonElement> FindProjectAsync(TestUser user, string marker, Guid projectId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{ProjectsPath}?search={marker}", user);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        return body.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == projectId).Clone();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, TestUser user, object? body = null) =>
        _api.SendAsync(method, path, user.Token, body, _ct);

    private async Task<Guid> CreateProjectAsync(TestUser user, string name)
    {
        using var response = await SendAsync(HttpMethod.Post, ProjectsPath, user, new { name });
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
