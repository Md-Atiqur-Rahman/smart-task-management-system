using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Dashboard;

[Collection(AuthCollectionDefinition.Name)]
public sealed class DashboardEndpointsTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private const string SummaryPath = "/api/dashboard/summary";
    private const string UpcomingPath = "/api/dashboard/upcoming";
    private const string ProjectsPath = "/api/projects";

    private readonly AuthApi _api = new(database, redis.ConnectionString);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Summary_AsNewTeamMember_Returns200WithZerosAndAllKeys()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);

        using var response = await _api.SendAsync(HttpMethod.Get, SummaryPath, member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("totalProjects").GetInt32().ShouldBe(0);
        body.GetProperty("totalTasks").GetInt32().ShouldBe(0);
        body.GetProperty("completedTasks").GetInt32().ShouldBe(0);
        body.GetProperty("pendingTasks").GetInt32().ShouldBe(0);
        body.GetProperty("overdueTasks").GetInt32().ShouldBe(0);
        var byStatus = body.GetProperty("tasksByStatus");
        foreach (var key in new[] { "toDo", "inProgress", "completed", "cancelled" })
        {
            byStatus.GetProperty(key).GetInt32().ShouldBe(0);
        }

        var byPriority = body.GetProperty("tasksByPriority");
        foreach (var key in new[] { "low", "medium", "high", "critical" })
        {
            byPriority.GetProperty(key).GetInt32().ShouldBe(0);
        }
    }

    [Fact]
    public async Task Summary_AfterPmCreatesAndAssignsTask_TeamMemberSeesOne()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pm);
        await CreateTaskAsync(pm, projectId, new { title = "Assigned", priority = "High", assigneeId = member.Id });

        using var response = await _api.SendAsync(HttpMethod.Get, SummaryPath, member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("totalProjects").GetInt32().ShouldBe(1);
        body.GetProperty("totalTasks").GetInt32().ShouldBe(1);
        body.GetProperty("pendingTasks").GetInt32().ShouldBe(1);
        body.GetProperty("tasksByStatus").GetProperty("toDo").GetInt32().ShouldBe(1);
        body.GetProperty("tasksByPriority").GetProperty("high").GetInt32().ShouldBe(1);
        body.GetProperty("tasksByPriority").GetProperty("critical").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Upcoming_TaskDueTomorrow_IsListedForAssignee()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pm);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var id = await CreateTaskAsync(
            pm,
            projectId,
            new { title = "Soon", priority = "Critical", dueDate = tomorrow, assigneeId = member.Id });

        using var response = await _api.SendAsync(HttpMethod.Get, UpcomingPath, member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetArrayLength().ShouldBe(1);
        var item = body[0];
        item.GetProperty("id").GetGuid().ShouldBe(id);
        item.GetProperty("title").GetString().ShouldBe("Soon");
        item.GetProperty("dueDate").GetString().ShouldBe(tomorrow);
        item.GetProperty("status").GetString().ShouldBe("ToDo");
        item.GetProperty("priority").GetString().ShouldBe("Critical");
        item.GetProperty("projectId").GetGuid().ShouldBe(projectId);
        item.GetProperty("assigneeId").GetGuid().ShouldBe(member.Id);
    }

    [Fact]
    public async Task Summary_NoToken_Returns401()
    {
        using var response = await _api.Client.GetAsync(AuthApi.Uri(SummaryPath), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upcoming_NoToken_Returns401()
    {
        using var response = await _api.Client.GetAsync(AuthApi.Uri(UpcomingPath), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _api.Dispose();

    private async Task<Guid> CreateProjectAsync(TestUser user)
    {
        using var response = await _api.SendAsync(HttpMethod.Post, ProjectsPath, user.Token, new { name = "Dashboard project" }, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTaskAsync(TestUser user, Guid projectId, object body)
    {
        using var response = await _api.SendAsync(HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", user.Token, body, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }
}
